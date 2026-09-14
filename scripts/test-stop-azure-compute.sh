#!/bin/bash
# Drives stop-azure-compute.sh against an az that only pretends, so every branch - including the one
# that takes production down - is exercised without touching Azure or costing a request.
set -u
script_directory="$(cd "$(dirname "$0")" && pwd)"
subject="$script_directory/stop-azure-compute.sh"
passed=0
failed=0

fake_azure_directory=$(mktemp -d)
cat > "$fake_azure_directory/az" <<'FAKE'
#!/bin/bash
# Answers the read-only queries with canned values, records every mutating call, and changes nothing.
state="$FAKE_AZURE_STATE"
record() { echo "$*" >> "$state/calls.txt"; }
case "$1 $2 $3" in
    "account show "*)
        [ -f "$state/signed-out" ] && exit 1
        if [[ "$*" == *"--query id"* ]]; then echo "00000000-1111-2222-3333-444444444444"
        else echo "Orbit pay-as-you-go"; fi
        exit 0 ;;
    "postgres flexible-server list") cat "$state/server-name.txt"; exit 0 ;;
    "postgres flexible-server show") echo "Ready"; exit 0 ;;
    "postgres flexible-server stop"|"postgres flexible-server start")
        record "$1 $2 $3"
        [ -f "$state/postgres-fails" ] && exit 1
        exit 0 ;;
    "containerapp ingress disable"|"containerapp ingress enable") record "$*"; exit 0 ;;
    "containerapp update -n") record "$*"; exit 0 ;;
    "containerapp show -n")
        if [[ "$*" == *"minReplicas"* ]]; then echo "1"; else echo "true"; fi
        exit 0 ;;
    "containerapp replica list") echo "1"; exit 0 ;;
    "rest --method get")
        [ -f "$state/no-budget" ] && exit 1
        printf '90\t61.40\tPLN\n'; exit 0 ;;
esac
exit 0
FAKE
chmod +x "$fake_azure_directory/az"
trap 'rm -rf "$fake_azure_directory"' EXIT

check() { # name expected actual
    if [ "$2" = "$3" ]; then
        echo "  ok    $1"; passed=$((passed + 1))
    else
        echo "  FAIL  $1: got [$3], expected [$2]"; failed=$((failed + 1))
    fi
}

check_contains() { # name needle haystack
    case "$3" in
        *"$2"*) echo "  ok    $1"; passed=$((passed + 1)) ;;
        *) echo "  FAIL  $1: [$2] missing from output"; failed=$((failed + 1)) ;;
    esac
}

# A fresh call log and a fresh set of flags per test, so one test's failure injection cannot leak into
# the next. Separate from run_subject because that one is usually called in a command substitution,
# where an assignment would not survive the subshell.
new_state() { # flag files to touch...
    export FAKE_AZURE_STATE
    FAKE_AZURE_STATE=$(mktemp -d)
    echo "orbit-postgres-djgiwo" > "$FAKE_AZURE_STATE/server-name.txt"
    : > "$FAKE_AZURE_STATE/calls.txt"
    while [ $# -gt 0 ]; do touch "$FAKE_AZURE_STATE/$1"; shift; done
}

run_subject() { # arguments...
    PATH="$fake_azure_directory:$PATH" "$subject" "$@" 2>&1
}

calls() { cat "$FAKE_AZURE_STATE/calls.txt"; }
call_count() { grep -c . "$FAKE_AZURE_STATE/calls.txt"; }

echo "--dry-run changes nothing"
new_state
output=$(run_subject --dry-run)
check "no mutating call reached az" "0" "$(call_count)"
check_contains "says what it would stop" "would run: az postgres flexible-server stop" "$output"
check_contains "would close both ingresses" "would run: az containerapp ingress disable -n orbit-web" "$output"

echo "--yes stops the database and both apps"
new_state
run_subject --yes > /dev/null
check "five mutating calls" "5" "$(call_count)"
check_contains "stops PostgreSQL" "postgres flexible-server stop" "$(calls)"
check_contains "closes the api ingress" "containerapp ingress disable -n orbit-api" "$(calls)"
check_contains "closes the web ingress" "containerapp ingress disable -n orbit-web" "$(calls)"
check_contains "empties orbit-api" "containerapp update -n orbit-api -g Orbit --min-replicas 0" "$(calls)"
check_contains "empties orbit-web" "containerapp update -n orbit-web -g Orbit --min-replicas 0" "$(calls)"

echo "--resume puts back exactly what azure-setup.md records"
new_state
run_subject --resume --yes > /dev/null
check "five mutating calls" "5" "$(call_count)"
check_contains "starts PostgreSQL first" "postgres flexible-server start" "$(head -1 "$FAKE_AZURE_STATE/calls.txt")"
check_contains "orbit-api keeps a warm replica" "containerapp update -n orbit-api -g Orbit --min-replicas 1" "$(calls)"
check_contains "orbit-web goes back to scaling to zero" "containerapp update -n orbit-web -g Orbit --min-replicas 0" "$(calls)"
check_contains "api ingress on 8080" "ingress enable -n orbit-api -g Orbit --type external --target-port 8080" "$(calls)"
check_contains "web ingress on 80" "ingress enable -n orbit-web -g Orbit --type external --target-port 80" "$(calls)"
check_contains "max-replicas is left alone" "0" "$(grep -c -- '--max-replicas' "$FAKE_AZURE_STATE/calls.txt")"

echo "--status only reads"
new_state
output=$(run_subject --status)
check "no mutating call reached az" "0" "$(call_count)"
check_contains "reports the spend against the budget" "61.40 of 90 PLN spent this month" "$output"
check_contains "reports the database state" "orbit-postgres-djgiwo: Ready" "$output"

echo "--status without a budget says so instead of pretending"
new_state no-budget
output=$(run_subject --status)
check_contains "names the missing budget" "No budget named orbit-monthly-budget" "$output"

echo "a failing command is reported, and the rest still run"
new_state postgres-fails
output=$(run_subject --yes)
check "exits non-zero" "1" "$?"
check_contains "says which command failed" "FAILED: az postgres flexible-server stop" "$output"
check_contains "still empties orbit-web" "containerapp update -n orbit-web -g Orbit --min-replicas 0" "$(calls)"

echo "signed out, it stops before touching anything"
new_state signed-out
output=$(run_subject --yes)
check "exits non-zero" "1" "$?"
check "no mutating call reached az" "0" "$(call_count)"
check_contains "says to sign in" "Run 'az login' first" "$output"

echo "an unknown option is refused rather than guessed at"
new_state
output=$(run_subject --stop-everything-now)
check "exits 2" "2" "$?"

echo
echo "$passed passed, $failed failed"
[ "$failed" -eq 0 ]
