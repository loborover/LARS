FROM python:3.11-slim

WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY . .

# piper 바이너리 및 ffmpeg 설치
RUN apt-get update && apt-get install -y wget ffmpeg curl && \
    wget -O /usr/local/bin/piper https://github.com/rhasspy/piper/releases/latest/download/piper_linux_x86_64 && \
    chmod +x /usr/local/bin/piper

EXPOSE 8088

CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8088"]
