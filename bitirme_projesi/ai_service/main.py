"""
Yerel CLIP görsel embedding API — sentence-transformers/clip-ViT-B-32 (512 boyut).
Çalıştırma: uvicorn main:app --host 0.0.0.0 --port 8000
"""

from contextlib import asynccontextmanager
import io
from typing import Optional

import numpy as np
from fastapi import FastAPI, HTTPException, Request
from fastapi.middleware.cors import CORSMiddleware
from PIL import Image
from sentence_transformers import SentenceTransformer

MODEL_NAME = "sentence-transformers/clip-ViT-B-32"
EXPECTED_DIM = 512

_model: Optional[SentenceTransformer] = None


@asynccontextmanager
async def lifespan(app: FastAPI):
    global _model
    _model = SentenceTransformer(MODEL_NAME)
    yield


app = FastAPI(title="CLIP Embedding Service", lifespan=lifespan)
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


@app.get("/health")
async def health():
    return {"status": "ok", "model": MODEL_NAME, "dim": EXPECTED_DIM}


@app.post("/embed")
async def embed(request: Request):
    """
    Ham görsel baytlarını gövdede kabul eder (Content-Type: image/jpeg vb. veya application/octet-stream).
    Yanıt: {"embedding": [512 float]}
    """
    if _model is None:
        raise HTTPException(status_code=503, detail="Model yüklenmedi.")

    raw = await request.body()
    if not raw:
        raise HTTPException(status_code=400, detail="Boş gövde.")

    try:
        img = Image.open(io.BytesIO(raw)).convert("RGB")
    except Exception:
        raise HTTPException(status_code=400, detail="Geçersiz görsel baytları.")

    vec = _model.encode(img, normalize_embeddings=True)
    arr = np.asarray(vec, dtype=np.float32).reshape(-1)

    if arr.size != EXPECTED_DIM:
        raise HTTPException(
            status_code=500,
            detail=f"Beklenen boyut {EXPECTED_DIM}, gelen {arr.size}",
        )

    return {"embedding": arr.tolist()}


if __name__ == "__main__":
    import uvicorn

    uvicorn.run("main:app", host="0.0.0.0", port=8000, reload=False)
