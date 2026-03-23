from fastapi import FastAPI
from pydantic import BaseModel
from transformers import AutoTokenizer, AutoModelForSequenceClassification, pipeline
import torch

app = FastAPI(title="FinBERT Sentiment API")

device = 0 if torch.cuda.is_available() else -1
model_name = "ProsusAI/finbert"
tokenizer = AutoTokenizer.from_pretrained(model_name)
model = AutoModelForSequenceClassification.from_pretrained(model_name)
sentiment_pipeline = pipeline("sentiment-analysis", model=model, tokenizer=tokenizer, device=device)

class TextRequest(BaseModel):
    text: str

class BatchRequest(BaseModel):
    texts: list[str]

@app.get("/health")
def health():
    return {"status": "ok", "gpu": torch.cuda.is_available(), "device": str(torch.cuda.get_device_name(0)) if torch.cuda.is_available() else "cpu"}

@app.post("/predict")
def predict(req: TextRequest):
    result = sentiment_pipeline(req.text)
    return result[0]

@app.post("/predict/batch")
def predict_batch(req: BatchRequest):
    results = sentiment_pipeline(req.texts)
    return results
