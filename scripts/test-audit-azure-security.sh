#!/bin/bash
# Drives audit-azure-security.sh against an az that only pretends, so each verdict is exercised against
# a known configuration - including the misconfigured one, which nobody wants to create in Azure to see.
set -u
script_directory="$(cd "$(dirname "$0")" && pwd)"
subject="$script_directory/audit-azure-security.sh"
passed=0
failed=0

fake_azure_directory=$(mktemp -d)
cat > "$fake_azure_directory/az" <<'FAKE'
#!/bin/bash
# Answers every read the audit makes out of files the test wrote, so one fake serves both the hardened
# and the unhardened subscription.
state="$FAKE_AZURE_STATE"
answer() { cat "$state/$1" 2>/dev/null; }
case "$1 $2 $3" in
    "account show "*)
        if [[ "$*" == *"--query id"* ]]; then echo "00000000-1111-2222-3333-444444444444"
        else echo "Azure subscription 1"; fi
        exit 0 ;;
    "security secure-scores list") answer secure-score; exit 0 ;;
    "security assessment list") answer assessments; exit 0 ;;
    "security contact list") answer security-contact; exit 0 ;;
    "acr show -n")
        if [[ "$*" == *"adminUserEnabled"* ]]; then answer acr-admin; else echo "Enabled"; fi
        exit 0 ;;
    "postgres flexible-server list") echo "orbit-postgres-djgiwo"; exit 0 ;;
    "postgres flexible-server show") echo "Enabled"; exit 0 ;;
    "postgres flexible-server parameter") answer secure-transport; exit 0 ;;
    "postgres flexible-server firewall-rule") answer firewall-rules; exit 0 ;;
    "storage account list") answer storage-accounts; exit 0 ;;
    "storage account show")
        account=""
        for argument in "$@"; do [ "$previous" = "-n" ] 2>/dev/null && account="$argument"; previous="$argument"; done
        if [[ "$*" == *"allowBlobPublicAccess"* ]]; then answer "$account-public-blobs"
        elif [[ "$*" == *"minimumTlsVersion"* ]]; then answer "$account-minimum-tls"
        elif [[ "$*" == *"supportsHttpsTrafficOnly"* ]]; then answer "$account-https-only"
        elif [[ "$*" == *"allowSharedKeyAccess"* ]]; then echo "false"; fi
        exit 0 ;;
    "containerapp show -n")
        if [[ "$*" == *"allowInsecure"* ]]; then answer allow-insecure
        elif [[ "$*" == *"transport"* ]]; then echo "auto"
        elif [[ "$*" == *"keyVaultUrl"* ]]; then echo "2"; fi
        exit 0 ;;
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

check_absent() { # name needle haystack
    case "$3" in
        *"$2"*) echo "  FAIL  $1: [$2] should not be in the output"; failed=$((failed + 1)) ;;
        *) echo "  ok    $1"; passed=$((passed + 1)) ;;
    esac
}

# The subscription as info/azure-security.md says it should end up: everything decided, nothing to fix.
hardened_state() {
    export FAKE_AZURE_STATE
    FAKE_AZURE_STATE=$(mktemp -d)
    echo "0.42" > "$FAKE_AZURE_STATE/secure-score"
    : > "$FAKE_AZURE_STATE/assessments"
    echo "owner@example.test" > "$FAKE_AZURE_STATE/security-contact"
    echo "false" > "$FAKE_AZURE_STATE/acr-admin"
    echo "on" > "$FAKE_AZURE_STATE/secure-transport"
    printf 'AllowAllAzureServicesAndResourcesWithinAzureIps\t0.0.0.0\t0.0.0.0\n' > "$FAKE_AZURE_STATE/firewall-rules"
    echo "orbitdownloads" > "$FAKE_AZURE_STATE/storage-accounts"
    echo "true" > "$FAKE_AZURE_STATE/orbitdownloads-public-blobs"
    echo "TLS1_2" > "$FAKE_AZURE_STATE/orbitdownloads-minimum-tls"
    echo "true" > "$FAKE_AZURE_STATE/orbitdownloads-https-only"
    echo "false" > "$FAKE_AZURE_STATE/allow-insecure"
}

run_subject() { PATH="$fake_azure_directory:$PATH" "$subject" "$@" 2>&1; }

echo "a subscription hardened the way azure-security.md describes"
hardened_state
output=$(run_subject)
check "exits clean" "0" "$?"
check_contains "no findings" "No findings against what info/azure-security.md decided" "$output"
check_contains "the public APK blob reads as a decision, not a fault" "decided  orbitdownloads serves the APK anonymously" "$output"
check_absent "the APK blob is not also reported as a fix" "FIX      orbitdownloads allows anonymous blob reads" "$output"
check_contains "says a clean run is not the same as secure" "That is not the same as secure" "$output"

echo "the registry's admin account still on"
hardened_state
echo "true" > "$FAKE_AZURE_STATE/acr-admin"
output=$(run_subject --resources)
check "exits non-zero" "1" "$?"
check_contains "names the admin account" "FIX      orbitcontainerregistry has the admin account enabled" "$output"

echo "a database that does not require TLS, behind a firewall somebody widened"
hardened_state
echo "off" > "$FAKE_AZURE_STATE/secure-transport"
printf 'AllowAllAzureServicesAndResourcesWithinAzureIps\t0.0.0.0\t0.0.0.0\nSomebodysLaptop\t81.0.0.1\t81.0.0.255\n' > "$FAKE_AZURE_STATE/firewall-rules"
output=$(run_subject --resources)
check_contains "flags the missing TLS requirement" "FIX      orbit-postgres-djgiwo requires TLS - is 'off'" "$output"
check_contains "flags the wider firewall rule" "1 firewall rule(s) open an address range beyond Azure's own" "$output"
check_contains "still lists the rules" "SomebodysLaptop" "$output"

echo "a storage account nobody meant to leave open"
hardened_state
printf 'orbitdownloads\norbitb722\n' > "$FAKE_AZURE_STATE/storage-accounts"
echo "true" > "$FAKE_AZURE_STATE/orbitb722-public-blobs"
echo "TLS1_0" > "$FAKE_AZURE_STATE/orbitb722-minimum-tls"
echo "false" > "$FAKE_AZURE_STATE/orbitb722-https-only"
output=$(run_subject --resources)
check_contains "flags anonymous reads on the account with no reason for them" "FIX      orbitb722 allows anonymous blob reads" "$output"
check_contains "flags the TLS floor" "FIX      orbitb722 floors TLS at 1.2 - is 'TLS1_0'" "$output"
check_contains "flags plain HTTP" "FIX      orbitb722 requires HTTPS - is 'false'" "$output"
check_absent "orbitdownloads is still a decision" "FIX      orbitdownloads allows anonymous" "$output"

echo "an app left accepting plain HTTP"
hardened_state
echo "true" > "$FAKE_AZURE_STATE/allow-insecure"
output=$(run_subject --resources)
check_contains "flags it on orbit-api" "FIX      orbit-api refuses plain HTTP" "$output"
check_contains "flags it on orbit-web" "FIX      orbit-web refuses plain HTTP" "$output"

echo "no security contact, and Defender listing what it found"
hardened_state
: > "$FAKE_AZURE_STATE/security-contact"
printf 'Storage accounts should restrict network access\nMFA should be enabled on accounts with owner permissions\n' > "$FAKE_AZURE_STATE/assessments"
output=$(run_subject --defender)
check_contains "flags the missing contact" "FIX      No security contact on the subscription" "$output"
check_contains "lists the failing assessments" "- MFA should be enabled on accounts with owner permissions" "$output"
check_contains "reports the score" "Secure score: 0.42" "$output"

echo "--defender does not touch the resource checks, and vice versa"
hardened_state
output=$(run_subject --defender)
check_absent "no registry section" "Container registry" "$output"
output=$(run_subject --resources)
check_absent "no Defender section" "Defender for Cloud" "$output"

echo "an unknown option is refused rather than guessed at"
output=$(run_subject --fix-everything)
check "exits 2" "2" "$?"

echo
echo "$passed passed, $failed failed"
[ "$failed" -eq 0 ]
