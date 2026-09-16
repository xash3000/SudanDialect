from optimum.onnxruntime import ORTModelForFeatureExtraction
from transformers import AutoTokenizer

model_id = "silma-ai/silma-embedding-matryoshka-v0.1"
save_dir = "./onnx-model-output"

print(f"Downloading and exporting {model_id} to ONNX...")

# Load the tokenizer
tokenizer = AutoTokenizer.from_pretrained(model_id)

# Loading with export=True handles the ONNX conversion automatically
model = ORTModelForFeatureExtraction.from_pretrained(model_id, export=True)

# Save both to your output directory
tokenizer.save_pretrained(save_dir)
model.save_pretrained(save_dir)

print("Export complete!")
