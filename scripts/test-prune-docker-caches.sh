#!/bin/bash
# Drives prune-docker-caches.sh against a docker that only pretends, so every branch - including the one
# that removes images - is exercised without removing anything real.
set -u
script_directory="$(cd "$(dirname "$0")" && pwd)"
subject="$script_directory/prune-docker-caches.sh"
passed=0
failed=0

fake_docker_directory=$(mktemp -d)
cat > "$fake_docker_directory/docker" <<'FAKE'
#!/bin/bash
# Reports whatever sizes the test asked for, records what it was told to remove, and removes nothing.
state="$FAKE_DOCKER_STATE"
record() { echo "$*" >> "$state/calls.txt"; }
case "$1 $2" in
  "system df")
    [ -f "$state/unreachable" ] && exit 1
    cat "$state/df.json"; exit 0 ;;
  "builder prune") record "$*"; cp "$state/after-builder.json" "$state/df.json"; exit 0 ;;
  "image prune")
    if [[ "$*" == *"-af"* ]]; then record "image prune all"; cp "$state/after-image-all.json" "$state/df.json"
    else record "image prune dangling"; cp "$state/after-image-dangling.json" "$state/df.json"; fi
    exit 0 ;;
esac
exit 0
FAKE
chmod +x "$fake_docker_directory/docker"
trap 'rm -rf "$fake_docker_directory"' EXIT

check() { # name expected actual
    if [ "$2" = "$3" ]; then
        echo "  ok    $1"; passed=$((passed + 1))
    else
        echo "  FAIL  $1: got [$3], expected [$2]"; failed=$((failed + 1))
    fi
}

df_json() { # images build-cache volumes
    cat <<JSON
{"Active":"5","Reclaimable":"0B (0%)","Size":"$1","TotalCount":"5","Type":"Images"}
{"Active":"0","Reclaimable":"0B (0%)","Size":"200.7kB","TotalCount":"5","Type":"Containers"}
{"Active":"4","Reclaimable":"0B (0%)","Size":"$3","TotalCount":"4","Type":"Local Volumes"}
{"Active":"0","Reclaimable":"$2 (100%)","Size":"$2","TotalCount":"854","Type":"Build Cache"}
JSON
}

# images cache volumes after-builder after-dangling after-all [options...]
run() {
    state=$(mktemp -d)
    export FAKE_DOCKER_STATE="$state"
    df_json "$1" "$2" "$3" > "$state/df.json"
    df_json "$4" "0B" "$3" > "$state/after-builder.json"
    df_json "$5" "0B" "$3" > "$state/after-image-dangling.json"
    df_json "$6" "0B" "$3" > "$state/after-image-all.json"
    shift 6
    out=$(PATH="$fake_docker_directory:$PATH" bash "$subject" "$@" 2>&1)
    # Absent when the run pruned nothing, which several of these cases are about.
    calls=$([ -f "$state/calls.txt" ] && tr '\n' ',' < "$state/calls.txt")
}

echo "--- under the threshold ---"
run "8GB" "2GB" "40GB" "8GB" "8GB" "8GB"
check "nothing is pruned" "" "$calls"
check "and it says so" "yes" "$(echo "$out" | grep -q 'Nothing done' && echo yes)"
# 40 GB of volumes is the local database. Counting it would have this clean up forever without ever
# getting under the threshold, since it is the one thing the script refuses to delete.
check "volumes are left out of the total" "yes" "$(echo "$out" | grep -q '10.00 GB' && echo yes)"

echo
echo "--- the build cache alone is usually enough ---"
run "8GB" "20GB" "40GB" "8GB" "8GB" "8GB"
check "the cache goes" "builder prune -af," "$calls"
check "and it stops there" "yes" "$(echo "$out" | grep -q 'now: 8.00 GB' && echo yes)"

echo
echo "--- orphaned layers next ---"
run "30GB" "20GB" "40GB" "30GB" "9GB" "9GB"
check "cache, then orphaned images" "builder prune -af,image prune dangling," "$calls"

echo
echo "--- and only then the images nothing is running ---"
run "40GB" "20GB" "40GB" "40GB" "40GB" "5GB"
check "all three rungs" "builder prune -af,image prune dangling,image prune all," "$calls"
check "the cost is named" "yes" "$(echo "$out" | grep -q 're-pull' && echo yes)"

echo
echo "--- a dry run changes nothing ---"
run "40GB" "20GB" "40GB" "40GB" "40GB" "5GB" --dry-run
check "nothing removed" "" "$calls"
check "but it says what it would do" "yes" "$(echo "$out" | grep -q 'Would prune' && echo yes)"

echo
echo "--- force, whatever the size ---"
run "1GB" "1GB" "40GB" "1GB" "1GB" "1GB" --force
check "prunes anyway" "builder prune -af," "$calls"

echo
echo "--- a threshold of one's own ---"
run "3GB" "7GB" "40GB" "3GB" "3GB" "3GB" --threshold-gigabytes 5
check "10 GB is over 5, and 3 GB is not" "builder prune -af," "$calls"

echo
echo "--- docker not running ---"
state=$(mktemp -d); export FAKE_DOCKER_STATE="$state"; touch "$state/unreachable"
out=$(PATH="$fake_docker_directory:$PATH" bash "$subject" 2>&1); exit_code=$?
check "exits quietly" "0" "$exit_code"
check "and says why" "yes" "$(echo "$out" | grep -q 'not answering' && echo yes)"

echo
echo "======================================================"
echo "prune-docker-caches.sh: $passed passed, $failed failed"
[ "$failed" -eq 0 ]
