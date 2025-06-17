import os
import io
import threading
import numpy as np
import sounddevice as sd
import soundfile as sf
import noisereduce as nr
from dotenv import load_dotenv
from openai import OpenAI
import keyboard
import json
import socket
from konlpy.tag import Okt
from parse_command import parse_command_from_keywords  # 별도 파일에서 명령 해석

load_dotenv()
client = OpenAI(api_key=os.getenv("OPENAI_API_KEY"))
okt = Okt()

FS = 16000
CHANNELS = 1
is_recording = False
audio_frames = []

UNITY_HOST = '127.0.0.1'
UNITY_PORT = 12345
unity_socket = None

def setup_unity_connection():
    global unity_socket
    try:
        unity_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        print(f"📡 Unity 통신 준비 완료 - {UNITY_HOST}:{UNITY_PORT}로 전송합니다")
        return True
    except Exception as e:
        print(f"❌ Unity 연결 설정 실패: {e}")
        return False

def audio_callback(indata, frames, time, status):
    if is_recording:
        audio_frames.append(indata.copy())

def extract_keywords_only(text):
    pos_tags = okt.pos(text)
    keywords = [word for word, tag in pos_tags if tag == 'Noun']
    return keywords

def send_to_unity(result_json):
    try:
        json_str = json.dumps(result_json, ensure_ascii=False)
        if unity_socket:
            unity_socket.sendto(json_str.encode('utf-8'), (UNITY_HOST, UNITY_PORT))
            print("🚀 Unity로 명령 전송 완료!", json_str)
        else:
            print("⚠️ Unity 소켓이 설정되지 않았습니다")
    except Exception as e:
        print(f"❌ Unity 전송 중 오류 발생: {e}")

def transcribe(audio):
    buffer = io.BytesIO()
    sf.write(buffer, audio, FS, format="WAV", subtype="PCM_16")
    buffer.name = "audio.wav"
    buffer.seek(0)

    resp = client.audio.transcriptions.create(
        model="whisper-1",
        file=buffer,
        response_format="json",
        prompt="보병, 궁병, 궁수, 적보병, 적궁병, 적궁수, 적기병, 적마법사, 기병, 적기병, 마법사, 검사, 활병, 기마병, 전진, 돌격, 전방, 후방, 빼, 위로, 뒤로, 정지, 멈춰춰"
    )

    text = resp.text.strip()
    print(f"📝 인식 결과: {text}")

    keywords = extract_keywords_only(text)
    print("🔑 추출 키워드:", ", ".join(keywords) if keywords else "없음")

    result_json = parse_command_from_keywords(keywords)
    print("📦 최종 명령 JSON (한글):", result_json)

    # 한글 병종명 → 영어 병종명 변환
    unit_output_mapping = {
        '보병': 'infantry',
        '궁병': 'bowman',
        '기병': 'cavalry',
        '마법사': 'mage'
    }
    mapped_json = {}
    for k, v in result_json.items():
        if k in ('infantry', 'target') and v in unit_output_mapping:
            mapped_json[k] = unit_output_mapping[v]
        else:
            mapped_json[k] = v
    print("📦 전송할 명령 JSON (영문):", mapped_json)

    send_to_unity(mapped_json)

def handle_manual_input():
    text = input("🧾 직접 명령어를 입력하세요: ").strip()
    if not text:
        print("⚠️ 입력이 비었습니다.")
        return

    print(f"📝 입력한 텍스트: {text}")
    keywords = extract_keywords_only(text)
    print("🔑 추출 키워드:", ", ".join(keywords) if keywords else "없음")

    result_json = parse_command_from_keywords(keywords)
    print("📦 최종 명령 JSON (한글):", result_json)

    # 한글 병종명 → 영어 병종명 변환
    unit_output_mapping = {
        '보병': 'infantry',
        '궁병': 'bowman',
        '기병': 'cavalry',
        '마법사': 'mage'
    }
    mapped_json = {}
    for k, v in result_json.items():
        if k in ('infantry', 'target') and v in unit_output_mapping:
            mapped_json[k] = unit_output_mapping[v]
        else:
            mapped_json[k] = v
    print("📦 전송할 명령 JSON (영문):", mapped_json)

    send_to_unity(mapped_json)

def record_control():
    global is_recording, audio_frames

    print("⏺️ 스페이스바를 누르면 녹음 시작 / 종료합니다. (Ctrl+C로 종료)")
    print("⌨️ v 키를 누르면 텍스트를 직접 입력하여 명령을 전송합니다.")
    with sd.InputStream(samplerate=FS, channels=CHANNELS, dtype='int16', callback=audio_callback):
        while True:
            event = keyboard.read_event()
            if event.event_type == keyboard.KEY_DOWN:
                if event.name == 'space':
                    if not is_recording:
                        print("▶ 녹음 시작!")
                        is_recording = True
                        audio_frames = []
                    else:
                        print("⏹️ 녹음 중지!")
                        is_recording = False
                        if audio_frames:
                            audio = np.concatenate(audio_frames, axis=0)
                            audio_flat = audio.flatten()
                            reduced_audio = nr.reduce_noise(y=audio_flat, sr=FS)
                            reduced_audio = reduced_audio.reshape(-1, 1)
                            threading.Thread(target=transcribe, args=(reduced_audio,)).start()
                elif event.name == 'v':
                    handle_manual_input()

def main():
    try:
        setup_unity_connection()
        record_control()
    except KeyboardInterrupt:
        print("\n🛑 종료합니다. 안녕!")
    finally:
        if unity_socket:
            unity_socket.close()
            print("🔌 Unity 연결을 닫았습니다")

if __name__ == "__main__":
    main()
