"""Расшифровывает звук из аудио или видео в текст с временными метками.

Запуск: .venv\\Scripts\\python.exe transcribe.py <файл> [--language ru] [--model large-v3]
Результат: <файл>.transcript.json и <файл>.transcript.txt рядом с исходником.
Сначала пробует видеокарту, при сбое CUDA считает на процессоре.
"""

import argparse
import json
import os
import sys
import time
import traceback

HERE = os.path.dirname(os.path.abspath(__file__))

# Кэш Hugging Face остаётся рядом со скриптом, а не в профиле пользователя на системном диске
os.environ.setdefault("HF_HOME", os.path.join(HERE, ".cache", "hf"))


def add_cuda_dll_dirs():
    """Библиотеки cuBLAS и cuDNN из pip лежат в site-packages\\nvidia, их нужно показать загрузчику DLL."""
    base = os.path.join(sys.prefix, "Lib", "site-packages", "nvidia")
    for sub in ("cublas", "cudnn"):
        path = os.path.join(base, sub, "bin")
        if os.path.isdir(path):
            os.add_dll_directory(path)
            os.environ["PATH"] = path + os.pathsep + os.environ.get("PATH", "")


def run(path, model_name, language, device, compute_type):
    from faster_whisper import WhisperModel

    model = WhisperModel(model_name, device=device, compute_type=compute_type, download_root=os.path.join(HERE, "models"))
    segments, info = model.transcribe(path, language=language, vad_filter=True)
    items = [{"start": round(s.start, 2), "end": round(s.end, 2), "text": s.text.strip()} for s in segments]
    return items, info


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("file")
    parser.add_argument("--language", default="ru")
    parser.add_argument("--model", default="large-v3")
    parser.add_argument("--device", default="auto", choices=["auto", "cuda", "cpu"])
    args = parser.parse_args()

    add_cuda_dll_dirs()
    started = time.time()
    # int8_float16 вдвое экономит видеопамять: рядом может идти игра
    gpu = ("cuda", "int8_float16")
    cpu = ("cpu", "int8")
    attempts = [gpu, cpu] if args.device == "auto" else [gpu if args.device == "cuda" else cpu]

    items = info = used = None
    for device, compute_type in attempts:
        try:
            items, info = run(args.file, args.model, args.language, device, compute_type)
            used = f"{device}/{compute_type}"
            break
        except Exception as error:  # сбой CUDA бывает и при загрузке, и при первом проходе
            print(f"{device}: не получилось ({type(error).__name__}: {error})", file=sys.stderr)
            if os.environ.get("WHISPER_DEBUG"):
                traceback.print_exc()

    if items is None:
        sys.exit("Расшифровка не удалась ни на видеокарте, ни на процессоре")

    with open(args.file + ".transcript.json", "w", encoding="utf-8") as out:
        json.dump({"language": info.language, "duration": round(info.duration, 1), "segments": items}, out, ensure_ascii=False, indent=2)
    with open(args.file + ".transcript.txt", "w", encoding="utf-8") as out:
        out.write("\n".join(item["text"] for item in items))

    print(f"Готово: {used}, звук {info.duration:.1f} с, расчёт {time.time() - started:.1f} с, фрагментов {len(items)}")


if __name__ == "__main__":
    main()
