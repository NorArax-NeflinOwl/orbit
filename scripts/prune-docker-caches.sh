#!/bin/bash
# Frees the disk Docker has taken for caches, once it has taken more than is worth minding.
#
# Building this project's images repeatedly is what fills it: every `docker compose build` leaves
# another layer cache behind and orphans the image it replaced. Left alone that grows without limit -
# 27 GB of build cache and 54 orphaned images on the machine this was written for.
#
# What it will never touch: named volumes. That is where Postgres keeps the local database, it is data
# rather than cache, and no amount of "cleaning up" is worth it. `docker volume prune` and
# `docker system prune --volumes` do not appear anywhere in this file on purpose.
#
# Usage:
#   scripts/prune-docker-caches.sh                      # prune only if over the threshold
#   scripts/prune-docker-caches.sh --dry-run            # say what it would do, change nothing
#   scripts/prune-docker-caches.sh --threshold-gigabytes 10
#   scripts/prune-docker-caches.sh --force              # prune whatever the size
set -uo pipefail

threshold_gigabytes=25
is_dry_run=false
is_forced=false

while [ $# -gt 0 ]; do
    case "$1" in
        --threshold-gigabytes) threshold_gigabytes="${2:-}"; shift 2 ;;
        --dry-run) is_dry_run=true; shift ;;
        --force) is_forced=true; shift ;;
        -h|--help) sed -n '2,20p' "$0"; exit 0 ;;
        *) echo "Unknown option: $1" >&2; exit 2 ;;
    esac
done

say() { echo "$(date '+%Y-%m-%d %H:%M:%S')  $*"; }

if ! docker system df >/dev/null 2>&1; then
    say "Docker is not answering, so there is nothing to measure. Nothing done."
    exit 0
fi

# Images, containers and build cache - what pruning can actually reclaim. Volumes are deliberately left
# out of the total: counting data this script refuses to delete would have it clean up over and over
# without ever getting under the threshold.
docker_cache_gigabytes() {
    docker system df --format json 2>/dev/null | awk '
        BEGIN { total = 0 }
        /"Type":"Local Volumes"/ { next }
        {
            match($0, /"Size":"[^"]*"/)
            size = substr($0, RSTART + 8, RLENGTH - 9)
            unit = size; sub(/^[0-9.]+/, "", unit)
            number = size; sub(/[^0-9.].*$/, "", number)
            multiplier = 1
            if (unit == "kB") multiplier = 1000
            else if (unit == "MB") multiplier = 1000000
            else if (unit == "GB") multiplier = 1000000000
            else if (unit == "TB") multiplier = 1000000000000
            total += number * multiplier
        }
        END { printf "%.2f", total / 1000000000 }'
}

# What the whole of Docker is costing the disk, which is the number a person actually notices. Reported
# rather than compared against, since most of it can be the volumes above.
docker_disk_footprint() {
    local container_directory="$HOME/Library/Containers/com.docker.docker"
    [ -d "$container_directory" ] || { echo "unknown"; return; }
    du -sh "$container_directory" 2>/dev/null | awk '{print $1}'
}

is_over_threshold() {
    awk -v used="$1" -v limit="$threshold_gigabytes" 'BEGIN { exit !(used > limit) }'
}

used_gigabytes=$(docker_cache_gigabytes)
say "Docker caches: ${used_gigabytes} GB (threshold ${threshold_gigabytes} GB). On disk, all of Docker: $(docker_disk_footprint)."

if ! is_over_threshold "$used_gigabytes" && [ "$is_forced" = false ]; then
    say "Under the threshold. Nothing done."
    exit 0
fi

if [ "$is_dry_run" = true ]; then
    say "Over the threshold. Would prune the build cache, then orphaned images, then unused ones."
    exit 0
fi

# Cheapest first, and it stops as soon as it is enough. Stopped containers are left alone: they are
# worth kilobytes, and taking them away makes `docker compose ps` look like the stack was never there.
say "Over the threshold. Pruning the build cache."
docker builder prune -af >/dev/null 2>&1
used_gigabytes=$(docker_cache_gigabytes)

# Layers left behind by a rebuild, belonging to no image anybody can name. Pure waste at any threshold.
if is_over_threshold "$used_gigabytes"; then
    say "Still ${used_gigabytes} GB. Removing orphaned image layers."
    docker image prune -f >/dev/null 2>&1
    used_gigabytes=$(docker_cache_gigabytes)
fi

# The last resort, and the only step with a cost: an image no container is using right now still has to
# be pulled or built again the next time the stack comes up.
if is_over_threshold "$used_gigabytes"; then
    say "Still ${used_gigabytes} GB. Removing images nothing is running, which will cost a re-pull."
    docker image prune -af >/dev/null 2>&1
    used_gigabytes=$(docker_cache_gigabytes)
fi

say "Docker caches now: ${used_gigabytes} GB. On disk, all of Docker: $(docker_disk_footprint)."
say "macOS returns the space as Docker Desktop trims its disk image, which can lag by a few minutes."
