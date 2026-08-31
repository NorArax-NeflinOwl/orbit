#!/usr/bin/env bash
# What version a project is at, and which commit it was built from.
#
# The number reads "version.patch.build", and the three parts answer three different questions:
#
#   version  The move to production. Orbit runs on a test environment today - publicly reachable, but a
#            test one - and this becomes 1 the day it moves to its own address. Set by hand in
#            version.props.
#   patch    A milestone, raised by hand when somebody decides one has been reached. Also version.props.
#            Deliberately not derivable: "far enough to matter" is a judgement, not a count.
#   build    Counted here: the number of days on which a commit landed on main touching this project,
#            since either number above was last changed. A day with five commits counts once, and a day
#            whose commits went nowhere near this project does not count at all.
#
# So nobody maintains the build number, the same commit always numbers itself the same, and raising a
# milestone starts the count again rather than carrying it forward.
#
# Counted against main, not against whatever is checked out: a build number is a claim about what has
# shipped, and commits on a branch have not. Working on a branch therefore reports the same number main
# would, which is what makes a local build comparable to a released one.
#
# Needs the full history. A shallow checkout (actions/checkout's default) can only see one commit and
# would number every build 0.2.1 - the script refuses rather than printing that.
#
# Usage: ci/compute-version.sh <web|mobile|api>
# Prints, for GITHUB_OUTPUT:
#   version=0.2.3
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
version_file="$repository_root/version.props"

read_number() {
    grep -o "<$1>[^<]*" "$version_file" | head -1 | cut -d'>' -f2
}

major=$(read_number OrbitMajorVersion)
patch=$(read_number OrbitPatchVersion)
if [ -z "$major" ] || [ -z "$patch" ]; then
    echo "error: version.props is missing OrbitMajorVersion or OrbitPatchVersion - a build cannot be numbered." >&2
    exit 1
fi

if [ "$(git -C "$repository_root" rev-parse --is-shallow-repository)" = "true" ]; then
    echo "error: this is a shallow clone, so the days cannot be counted. Check out with fetch-depth: 0." >&2
    exit 1
fi

# What "main" means from here. origin/main first, because that is what has actually shipped; the local
# branch when there is no remote; and HEAD as the last resort, which is what a fresh clone with no
# branches named main would leave.
for candidate in origin/main main HEAD; do
    if git -C "$repository_root" rev-parse --verify --quiet "$candidate" >/dev/null; then
        main_ref="$candidate"
        break
    fi
done

# Where this milestone began: the last commit on main that changed the two numbers. Everything before it
# belongs to a previous one and is not counted.
milestone_started_at=$(git -C "$repository_root" log --format=%H -1 "$main_ref" -- version.props)
range=${milestone_started_at:+$milestone_started_at..$main_ref}
range=${range:-$main_ref}

# Authored dates rather than committer dates: a rebase moves the second and would renumber history that
# has already shipped.
build=$(git -C "$repository_root" log --format=%ad --date=short "$range" -- "${paths[@]}" | sort -u | grep -c . || true)

echo "version=$major.$patch.$build"
echo "commit=$(git -C "$repository_root" rev-parse HEAD)"
