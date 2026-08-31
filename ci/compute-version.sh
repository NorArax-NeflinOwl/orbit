#!/usr/bin/env bash
# What version a project is at, and which commit it was built from.
#
# The patch number is the count of distinct days on which a commit touched that project. Nobody
# maintains it: it is a function of the history, so the same commit always numbers itself the same, two
# builds of it agree, and there is no file anybody can forget to bump. A day with five commits counts
# once, and a day whose commits went nowhere near this project does not count at all - which is the
# whole point of asking per project.
#
# Needs the full history. A shallow checkout (actions/checkout's default) can only see one commit and
# would number every build 0.1.1 - the script refuses rather than printing that.
#
# Usage: ci/compute-version.sh <web|mobile|api>
# Prints, for GITHUB_OUTPUT:
#   version=0.1.17
#   commit=51536f360a130d98b3b631da81dce22e38c0903a
set -euo pipefail

project="${1:-}"

case "$project" in
    # Each client counts the shared projects it compiles as its own: a change to Orbit.Core is a change
    # to every app built from it, and pretending otherwise would ship a new client under an old number.
    web)    paths=(src/Clients/Orbit.Web src/Shared) ;;
    mobile) paths=(src/Clients/Orbit.Mobile src/Clients/Orbit.Maui src/Shared) ;;
    api)    paths=(src/Server src/Shared) ;;
    *)
        echo "usage: ci/compute-version.sh <web|mobile|api>" >&2
        exit 2
        ;;
esac

repository_root=$(git rev-parse --show-toplevel)
series=$(grep -o '<OrbitVersionSeries>[^<]*' "$repository_root/Directory.Build.props" | head -1 | cut -d'>' -f2)
if [ -z "$series" ]; then
    echo "error: no <OrbitVersionSeries> in Directory.Build.props - a build cannot be numbered." >&2
    exit 1
fi

if [ "$(git -C "$repository_root" rev-parse --is-shallow-repository)" = "true" ]; then
    echo "error: this is a shallow clone, so the days cannot be counted. Check out with fetch-depth: 0." >&2
    exit 1
fi

# Authored dates rather than committer dates: a rebase moves the second and would renumber history that
# has already shipped.
days=$(git -C "$repository_root" log --format=%ad --date=short -- "${paths[@]}" | sort -u | grep -c .)

echo "version=$series.$days"
echo "commit=$(git -C "$repository_root" rev-parse HEAD)"
