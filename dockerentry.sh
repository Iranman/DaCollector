#!/bin/bash

echo "Started DaCollector bootstrapping process..."

# Set variable for the UID and GID based on env, else use default values
PUID=${PUID:-1000}
PGID=${PGID:-100}

GROUP="dacollectorgroup"
USER="dacollector"

# Well-known users.
if [ "$PUID" -eq 0 ]; then
    USER="root"
fi

# Well-known groups.
if [ "$PGID" -eq 0 ]; then
    GROUP="root"
elif [ "$PGID" -eq 100 ]; then
    GROUP="users"
fi

# Create or update group.
if [ $(getent group $GROUP) ]; then
    if [ $(getent group $GROUP | cut -d: -f3) -ne $PGID ]; then
        groupmod -g "$PGID" $GROUP
    fi
else
    groupadd -o -g "$PGID" $GROUP
fi

# Create or update user.
if [ $(getent passwd $USER) ]; then
    if [ $(getent passwd $USER | cut -d: -f3) -ne $PUID ]; then
        usermod -u "$PUID" $USER
    fi
    [ $(id -g $USER) -ne $PGID ] && usermod -g "$PGID" $USER
else
    echo "Creating user $USER (uid=$PUID, gid=$PGID)..."
    useradd  -N -o -u "$PUID" -g "$PGID" -d /home/dacollector $USER

    mkdir -p /home/dacollector/
    chown $USER:$GROUP /home/dacollector
fi

# Make sure DACOLLECTOR_HOME directory is correctly set.
DACOLLECTOR_HOME=${DACOLLECTOR_HOME:-/home/dacollector/.dacollector/DaCollector}
if [ "$PUID" -eq 0 ]; then
    if [ "$DACOLLECTOR_HOME" == "/home/dacollector/.dacollector/DaCollector" ]; then
        echo "Error: Cannot use default DACOLLECTOR_HOME directory when running as root (PUID=0)."
        echo "Please set a custom DACOLLECTOR_HOME directory."
        exit 1
    fi
fi
if [ ! -d "$DACOLLECTOR_HOME" ]; then
    echo "Creating DACOLLECTOR_HOME directory: $DACOLLECTOR_HOME"
    mkdir -p "$DACOLLECTOR_HOME"
fi

# Set ownership of application data to dacollector user.
# Set SKIP_CHOWN=true to bypass ownership repair (useful on TrueNAS/ZFS with ACL-managed datasets).
SKIP_CHOWN=${SKIP_CHOWN:-false}
OWNER=$(stat -c '%u:%g' "$DACOLLECTOR_HOME" 2>/dev/null)
if [ "$SKIP_CHOWN" = "true" ]; then
    echo "Ownership repair skipped (SKIP_CHOWN=true). Owner of $DACOLLECTOR_HOME: ${OWNER:-unknown}"
elif [ "$OWNER" != "$PUID:$PGID" ]; then
    echo "Ownership mismatch on $DACOLLECTOR_HOME"
    echo "  Current owner : ${OWNER:-unknown}"
    echo "  Expected owner: $PUID:$PGID"
    echo "Starting ownership repair. This may be slow on large datasets or ZFS/TrueNAS volumes."
    echo "Set SKIP_CHOWN=true to skip this step if you manage permissions externally."

    # Fix parent directories without recursion (fast)
    [ -d /home/dacollector ] && chown $PUID:$PGID /home/dacollector
    PARENT_DIR=$(dirname "$DACOLLECTOR_HOME")
    [ -d "$PARENT_DIR" ] && chown $PUID:$PGID "$PARENT_DIR"

    # Recursively fix only the DaCollector data directory (not unrelated paths under /home/dacollector)
    chown -R $PUID:$PGID "$DACOLLECTOR_HOME"
    echo "Ownership of $DACOLLECTOR_HOME repaired to $PUID:$PGID."
else
    echo "Ownership of $DACOLLECTOR_HOME is correct ($OWNER)."
fi

# Ensure the log directory exists and is writable by the container user.
# This runs regardless of SKIP_CHOWN so that a bind-mounted ./logs directory
# created by Docker (as root:root) is always accessible to the server process.
LOG_DIR="$DACOLLECTOR_HOME/logs"
mkdir -p "$LOG_DIR"
chown $PUID:$PGID "$LOG_DIR"

if [ -d /root/.dacollector ]; then
    echo "
-------------------------------------
OLD DACOLLECTOR INSTALL DETECTED

Please change the volume for DaCollector
OLD directory: /root/.dacollector
New directory: /home/dacollector/.dacollector
-------------------------------------
    "
    exit 1
fi

# set umask to specified value if defined
if [[ ! -z "${UMASK}" ]]; then
     umask "${UMASK}"
fi

echo "
-------------------------------------
User ID:   $(id -u $USER)
Group ID:  $(id -g $USER)
UMASK set: $(umask)
Directory: \"$DACOLLECTOR_HOME\"
Owner:     $(stat -c '%u:%g' "$DACOLLECTOR_HOME" 2>/dev/null || echo 'unknown')
-------------------------------------
"

# Allow/disallow the server to be shutdown/restarted from the web interface.
ENABLE_SHUTDOWN=${ENABLE_SHUTDOWN:-false}
ENABLE_RESTART=${ENABLE_RESTART:-true}

ARGS=""
[ "$ENABLE_SHUTDOWN" = "true" ] && ARGS="$ARGS --shutdown-enabled"
[ "$ENABLE_RESTART" = "true" ] && ARGS="$ARGS --restart-enabled"

# Run the server, and restart it if it exits with code 140 (Custom restart exit code).
# Set up signal forwarding to the dotnet process
trap 'kill -TERM $DOTNET_PID 2>/dev/null; exit 143' TERM INT

while true; do
  gosu $USER:$GROUP /app/DaCollector.CLI $ARGS &
  DOTNET_PID=$!
  wait $DOTNET_PID
  EXIT_CODE=$?
  [ $EXIT_CODE -ne 140 ] && exit $EXIT_CODE
done
