#!/bin/bash

echo "Started DaCollector bootstrapping process…"

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
    echo "Adding user $USER and changing ownership of /home/dacollector and all it's sub-directories…"
    useradd  -N -o -u "$PUID" -g "$PGID" -d /home/dacollector $USER

    mkdir -p /home/dacollector/
    chown $USER:$GROUP /home/dacollector
fi

# Make sure DACOLLECTOR_HOME directory is correctly set.
DACOLLECTOR_HOME=${DACOLLECTOR_HOME:-/home/dacollector/.dacollector/DaCollector.CLI}
if [ "$PUID" -eq 0 ]; then
    if [ "$DACOLLECTOR_HOME" == "/home/dacollector/.dacollector/DaCollector.CLI" ]; then
        echo "Error: Cannot use default DACOLLECTOR_HOME directory when running as root (PUID=0)."
        echo "Please set a custom DACOLLECTOR_HOME directory."
        exit 1
    fi
fi
if [ ! -d "$DACOLLECTOR_HOME" ]; then
    if [ "$DACOLLECTOR_HOME" == "/home/dacollector/.dacollector/DaCollector.CLI" ]; then
        echo "Creating default DACOLLECTOR_HOME directory: $DACOLLECTOR_HOME"
        mkdir -p "$DACOLLECTOR_HOME"
    else
        echo "Error: DACOLLECTOR_HOME directory ($DACOLLECTOR_HOME) does not exist!"
        exit 1
    fi
fi

# Set ownership of application data to dacollector user.
OWNER=$(stat -c '%u:%g' "$DACOLLECTOR_HOME" 2>/dev/null)
if [ "$OWNER" != "$PUID:$PGID" ]; then
    echo "Changing ownership of /home/dacollector and all it's sub-directories…"
    chown -R $PUID:$PGID /home/dacollector/
fi

# Set ownership of DaCollector files to dacollector user
chown -R $USER:$GROUP /usr/src/app/build/
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
  gosu $USER:$GROUP /usr/src/app/build/DaCollector.CLI $ARGS &
  DOTNET_PID=$!
  wait $DOTNET_PID
  EXIT_CODE=$?
  [ $EXIT_CODE -ne 140 ] && exit $EXIT_CODE
done
