FROM ubuntu:24.04 AS builder
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates git cmake build-essential && rm -rf /var/lib/apt/lists/*
RUN git clone --depth 1 --branch v1.8.7 https://github.com/ggml-org/whisper.cpp.git /whisper
RUN cmake -S /whisper -B /whisper/build -DCMAKE_BUILD_TYPE=Release -DGGML_NATIVE=OFF -DBUILD_SHARED_LIBS=OFF -DWHISPER_BUILD_TESTS=OFF \
    && cmake --build /whisper/build --target whisper-cli -j 2

FROM python:3.13-slim
RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg ca-certificates && rm -rf /var/lib/apt/lists/*
COPY --from=builder /whisper/build/bin/whisper-cli /usr/local/bin/whisper-cli
WORKDIR /runtime
COPY server.py ./
RUN useradd --uid 10001 --create-home transcriber && mkdir /models && chown transcriber:transcriber /models
USER transcriber
EXPOSE 8081
ENTRYPOINT ["python3", "server.py"]
