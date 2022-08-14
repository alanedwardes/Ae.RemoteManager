FROM --platform=linux/arm64/v8 mcr.microsoft.com/dotnet/runtime:6.0

ADD build/output /opt/remotemananger

VOLUME ["/data"]

WORKDIR /data

ENTRYPOINT ["/opt/remotemananger/Ae.RemoteManager.Console"]
