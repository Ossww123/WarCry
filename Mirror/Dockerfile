FROM ubuntu:22.04

# Unity 서버가 의존하는 라이브러리 설치
RUN apt-get update && apt-get install -y \
    libgtk-3-0 \
    libglu1-mesa \
    libasound2 \
    libnss3 \
    libnspr4 \
    libxtst6 \
    libxss1 \
    xvfb \
  && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# 빌드된 서버 바이너리 전체를 복사 (mirror_server 폴더에서)
COPY mirror_server/linux_server.x86_64 .
COPY mirror_server/linux_server_Data ./linux_server_Data
COPY mirror_server/libdecor-0.so.0 ./libdecor-0.so.0
COPY mirror_server/libdecor-cairo.so ./libdecor-cairo.so
COPY mirror_server/UnityPlayer.so ./UnityPlayer.so

# 실행권한 부여
RUN chmod +x linux_server.x86_64

# 빌드 아규먼트로 포트 지정 (기본 7777)
ARG PORT=7777
ENV PORT=${PORT}

# 컨테이너 내부에서 열어줄 포트
EXPOSE ${PORT}/tcp
EXPOSE ${PORT}/udp

# 쉘 형태 ENTRYPOINT로 $PORT 확장
ENTRYPOINT ["/bin/sh", "-c", "exec ./linux_server.x86_64 -batchmode -nographics -port $PORT -logFile /dev/stdout"]
