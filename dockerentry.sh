#!/bin/bash

cat << 'DACOLLECTOR_BANNER'

 ____         ____      _ _           _
|  _ \  __ _ / ___|___ | | | ___  ___| |_ ___  _ __
| | | |/ _` | |   / _ \| | |/ _ \/ __| __/ _ \| '__|
| |_| | (_| | |__| (_) | | |  __/ (__| || (_) | |
|____/ \__,_|\____\___/|_|_|\___|\___|\__\___/|_|

         Plex Movie & TV Collection Manager

DACOLLECTOR_BANNER

# UID/GID for the server process.  Defaults match a typical Linux install.
PUID=${PUID:-1000}
PGID=${PGID:-100}

# -----------------------------------------------------------------------
# User and group resolution
# When PUID=0 the server runs as root; no user/group creation is needed.
# -----------------------------------------------------------------------
if [ "$PUID" -eq 0 ]; then
    USER="root"
    GROUP="root"
else
    USER="dacollector"
    GROUP="dacollectorgroup"

    # Well-known groups
    if [ "$PGID" -eq 0 ]; then
        GROUP="root"
    elif [ "$PGID" -eq 100 ]; then
        GROUP="users"
    fi

    # Create or update group.
    # timeout 5 guards against getent hanging on LDAP/NIS-backed NSS (common on TrueNAS).
    GROUP_ENTRY=$(timeout 5 getent group $GROUP 2>/dev/null)
    if [ -n "$GROUP_ENTRY" ]; then
        EXISTING_GID=$(echo "$GROUP_ENTRY" | cut -d: -f3)
        if [ -n "$EXISTING_GID" ] && [ "$EXISTING_GID" -ne "$PGID" ]; then
            groupmod -g "$PGID" $GROUP
        fi
    else
        groupadd -o -g "$PGID" $GROUP
    fi

    # Create or update user.
    PASSWD_ENTRY=$(timeout 5 getent passwd $USER 2>/dev/null)
    if [ -n "$PASSWD_ENTRY" ]; then
        EXISTING_UID=$(echo "$PASSWD_ENTRY" | cut -d: -f3)
        if [ -n "$EXISTING_UID" ] && [ "$EXISTING_UID" -ne "$PUID" ]; then
            usermod -u "$PUID" $USER
        fi
        [ $(id -g $USER) -ne $PGID ] && usermod -g "$PGID" $USER
    else
        echo "Creating user $USER (uid=$PUID, gid=$PGID)..."
        useradd -N -o -u "$PUID" -g "$PGID" -d /home/dacollector $USER
        mkdir -p /home/dacollector/
        chown $USER:$GROUP /home/dacollector
    fi
fi

# -----------------------------------------------------------------------
# DACOLLECTOR_HOME setup
# -----------------------------------------------------------------------
DACOLLECTOR_HOME=${DACOLLECTOR_HOME:-/home/dacollector/.dacollector/DaCollector}
if [ ! -d "$DACOLLECTOR_HOME" ]; then
    echo "Creating DACOLLECTOR_HOME directory: $DACOLLECTOR_HOME"
    mkdir -p "$DACOLLECTOR_HOME"
fi

# -----------------------------------------------------------------------
# Ownership repair
#
# SKIP_CHOWN=true  → skip only the expensive recursive chown of existing
#                    data files inside DACOLLECTOR_HOME.  The top-level
#                    directory is still fixed so the server can create new
#                    files (e.g. settings-server.json) on first boot.
# SKIP_CHOWN=false → always run full recursive chown.
# (unset)          → auto-detect: ZFS/NFS/CIFS skip recursive chown;
#                    other filesystems run it when ownership is wrong.
#
# Root (PUID=0) always skips chown — root can write anywhere.
# -----------------------------------------------------------------------
if [ "$PUID" -eq 0 ]; then
    echo "Running as root (PUID=0); ownership repair skipped."
else
    OWNER=$(stat -c '%u:%g' "$DACOLLECTOR_HOME" 2>/dev/null)

    # Auto-detect slow/ACL-managed filesystems.
    if [ -z "$SKIP_CHOWN" ]; then
        FS_TYPE=$(df -T "$DACOLLECTOR_HOME" 2>/dev/null | awk 'NR==2 {print $2}')
        case "$FS_TYPE" in
            zfs|nfs|nfs4|cifs|smb*)
                echo "Filesystem '$FS_TYPE' detected on $DACOLLECTOR_HOME — recursive chown skipped automatically."
                SKIP_CHOWN=true
                ;;
            *)
                SKIP_CHOWN=false
                ;;
        esac
    fi

    if [ "$OWNER" = "$PUID:$PGID" ]; then
        echo "Ownership of $DACOLLECTOR_HOME is correct ($OWNER)."
    else
        echo "Ownership mismatch on $DACOLLECTOR_HOME (current: ${OWNER:-unknown}, expected: $PUID:$PGID)."

        # Always fix the top-level directory and its parents — one chown call
        # per path, instant even on ZFS/NFS.  This ensures the server can write
        # new files even when recursive chown is skipped.
        [ -d /home/dacollector ] && chown $PUID:$PGID /home/dacollector
        PARENT_DIR=$(dirname "$DACOLLECTOR_HOME")
        [ -d "$PARENT_DIR" ] && chown $PUID:$PGID "$PARENT_DIR"
        chown $PUID:$PGID "$DACOLLECTOR_HOME"

        if [ "$SKIP_CHOWN" = "true" ]; then
            echo "Top-level directory ownership set to $PUID:$PGID."
            echo "Recursive chown skipped (SKIP_CHOWN=true). Existing files inside $DACOLLECTOR_HOME keep their previous ownership."
            echo "If the server cannot read an existing file, fix permissions on the host or unset SKIP_CHOWN."
        else
            echo "Starting recursive ownership repair (this may be slow on large datasets)."
            echo "Set SKIP_CHOWN=true to skip this if you manage permissions externally."
            chown -R $PUID:$PGID "$DACOLLECTOR_HOME"
            echo "Ownership of $DACOLLECTOR_HOME repaired to $PUID:$PGID."
        fi
    fi

fi

# Always ensure the log directory exists.  Fix its ownership for non-root runs
# so that a bind-mounted ./logs directory created by Docker as root:root is
# accessible to the server process without requiring a full recursive chown.
LOG_DIR="$DACOLLECTOR_HOME/logs"
mkdir -p "$LOG_DIR"
if [ "$PUID" -ne 0 ]; then
    chown $PUID:$PGID "$LOG_DIR"
fi

# -----------------------------------------------------------------------
# Sanity check for old install path
# -----------------------------------------------------------------------
if [ -d /root/.dacollector ]; then
    echo ""
    echo "-------------------------------------"
    echo "OLD DACOLLECTOR INSTALL DETECTED"
    echo ""
    echo "Please change the volume for DaCollector"
    echo "OLD directory: /root/.dacollector"
    echo "New directory: /home/dacollector/.dacollector"
    echo "-------------------------------------"
    exit 1
fi

# Apply UMASK if set.
if [[ -n "${UMASK}" ]]; then
    umask "${UMASK}"
fi

echo ""
echo "-------------------------------------"
echo "User ID:   $(id -u $USER)"
echo "Group ID:  $(id -g $USER)"
echo "UMASK set: $(umask)"
echo "Directory: \"$DACOLLECTOR_HOME\""
echo "Owner:     $(stat -c '%u:%g' "$DACOLLECTOR_HOME" 2>/dev/null || echo 'unknown')"
echo "-------------------------------------"
echo ""

# -----------------------------------------------------------------------
# Start the server
# -----------------------------------------------------------------------
ENABLE_SHUTDOWN=${ENABLE_SHUTDOWN:-false}
ENABLE_RESTART=${ENABLE_RESTART:-true}

ARGS=""
[ "$ENABLE_SHUTDOWN" = "true" ] && ARGS="$ARGS --shutdown-enabled"
[ "$ENABLE_RESTART" = "true" ] && ARGS="$ARGS --restart-enabled"

trap 'kill -TERM $DOTNET_PID 2>/dev/null; exit 143' TERM INT

while true; do
    gosu $USER:$GROUP /app/DaCollector.CLI $ARGS &
    DOTNET_PID=$!
    wait $DOTNET_PID
    EXIT_CODE=$?
    [ $EXIT_CODE -ne 140 ] && exit $EXIT_CODE
done
