import os
import uuid
from fastapi import FastAPI, File, UploadFile
from fastapi.responses import JSONResponse
from fastapi.staticfiles import StaticFiles
from fastapi.middleware.cors import CORSMiddleware

app = FastAPI()

# CORS 설정 (Unity에서 접근할 거라 일단 전부 허용)
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# 업로드 폴더
UPLOAD_DIR = "uploads"
os.makedirs(UPLOAD_DIR, exist_ok=True)

# /uploads 경로로 정적 파일 서빙
app.mount("/uploads", StaticFiles(directory=UPLOAD_DIR), name="uploads")


@app.post("/upload")
async def upload_image(file: UploadFile = File(...)):
    # 파일 이름 / 확장자
    _, ext = os.path.splitext(file.filename)
    ext = ext.lower()

    if ext not in [".png", ".jpg", ".jpeg"]:
        return JSONResponse(
            {"error": "png, jpg, jpeg만 허용됩니다."},
            status_code=400
        )

    # 랜덤 파일명 생성
    filename = f"{uuid.uuid4().hex}{ext}"
    filepath = os.path.join(UPLOAD_DIR, filename)

    # 실제 파일 저장
    with open(filepath, "wb") as f:
        f.write(await file.read())

    # 이 URL이 나중에 Runway promptImage로 들어갈 값
    file_url = f"http://localhost:8000/uploads/{filename}"

    return {"url": file_url}
