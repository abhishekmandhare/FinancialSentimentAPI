from fastapi import FastAPI
from pydantic import BaseModel
from transformers import AutoTokenizer, AutoModelForSequenceClassification, pipeline
import torch

app = FastAPI(title="FinBERT Sentiment API")

device = 0 if torch.cuda.is_available() else -1
model_name = "ProsusAI/finbert"
tokenizer = AutoTokenizer.from_pretrained(model_name)
model = AutoModelForSequenceClassification.from_pretrained(model_name)
# top_k=None returns a score for every label (positive/negative/neutral).
# Without it the pipeline returns only the winning label, which breaks the
# consumer: it deserializes a JSON array and needs all three scores to compute
# score = P(pos) - P(neg) and confidence = 1 - P(neutral).
sentiment_pipeline = pipeline(
    "sentiment-analysis",
    model=model,
    tokenizer=tokenizer,
    device=device,
    top_k=None,
)

class TextRequest(BaseModel):
    text: str

class BatchRequest(BaseModel):
    texts: list[str]

@app.get("/health")
def health():
    return {"status": "ok", "gpu": torch.cuda.is_available(), "device": str(torch.cuda.get_device_name(0)) if torch.cuda.is_available() else "cpu"}

@app.post("/predict")
def predict(req: TextRequest):
    """Return every label score for one text, as a flat JSON array."""
    result = sentiment_pipeline(req.text)
    first = result[0] if result else []
    # transformers has shipped both [[...]] and [...] for this call.
    return first if isinstance(first, list) else result

@app.post("/predict/batch")
def predict_batch(req: BatchRequest):
    """Return every label score for each text: one array per input."""
    return sentiment_pipeline(req.texts)
