![logo](./frontend/public/favicon.ico)

# [sudandialect.com](https://sudandialect.com)

This project aims to preserve Sudan’s linguistic richness and cultural diversity through a modern digital platform. We believe the Sudanese dialect is more than a way of speaking; it is a record of long-standing cultural and linguistic exchange that deserves to be documented for future generations.

The project began as an effort to connect linguistic heritage with modern technology. Thousands of printed and scanned pages were transformed into a structured digital database, with programmatic data processing to improve search accuracy and make browsing easier.

This dictionary is an ongoing effort, and we welcome contributions and corrections to keep improving the content.


## Features

- **Advanced Arabic Search**: A robust search engine optimized for the Sudanese dialect, featuring:
  - **Text Normalization**: Automatic handling of arabic letters variants (أ، إ، آ), (ى، ئ), (ة, ه).
  - **Diacritics & Tashkeel Removal**: Seamlessly searches through text regardless of Tashkeel or other character extensions.
  - **Similarity Matching**: Powered by PostgreSQL trigram similarity to handle common spelling variations and typos.
- **Semantic Search**: Finds words by meaning rather than exact spelling, allowing natural language queries to discover related words and concepts. Powered by PostgreSQL Pgvector extension.
- **Alphabetical Browsing**: An interactive index allowing users to explore the dictionary letter by letter.

## Tech Stack

- Backend: C# (.NET 10), ASP.NET, Entity Framework Core, PostgreSQL with pgvector
- Frontend: Angular 21
- Semantic Search: silma-ai/silma-embedding-matryoshka-v0.1 embeddings model

## Run Locally

Prerequisites: .NET 10 SDK, Node 20+, Docker Engine, Python 3.10+ (only for exporting the embedding model).

Frontend and backend are separate.

### Frontend (Angular)

1. Open a terminal in `frontend/`.
2. Install dependencies:

```bash
npm install
```

3. Start the dev server:

```bash
ng serve
```

4. Open `http://localhost:4200`..

### Backend

1. Create a `.env` file in `backend/` with these variables:

```env
POSTGRES_USER=postgres
POSTGRES_PASSWORD=postgres
POSTGRES_DB=sudandialect
API_PORT=5038
JWT_SIGNING_KEY=replace-with-a-long-random-secret
TURNSTILE_SECRET_KEY=replace-with-your-turnstile-secret
ADMIN_USER_1=admin
ADMIN_PASS_1=replace-with-strong-password
FRONTEND_URL=http://localhost:4200
```

2. Set up the embedding model (required for semantic search):

```bash
cd backend/SudanDialect.Api/onnx_models
pip install "optimum[onnxruntime]" transformers
python export.py
# Copy the exported files from onnx-model-output/ into the root folder:
# silma-embedding-matryoshka-v0.1.onnx, vocab.txt, tokenizer.json
```

3. Build the API docker image:

```bash
cd backend
docker build -f SudanDialect.Api/Dockerfile -t ghcr.io/xash3000/sudandialect:latest .
```

4. Start backend services with Docker Compose (DB image is `pgvector/pgvector:pg18`):

```bash
docker compose up -d
```

5. Copy the model files into the API container volume and restart the API (run from repo root):

```bash
docker cp backend/SudanDialect.Api/onnx_models/silma-embedding-matryoshka-v0.1.onnx sudan_dialect_api:/app/onnx_models/
docker cp backend/SudanDialect.Api/onnx_models/vocab.txt sudan_dialect_api:/app/onnx_models/
docker cp backend/SudanDialect.Api/onnx_models/tokenizer.json sudan_dialect_api:/app/onnx_models/
docker restart sudan_dialect_api
```

6. Apply database migrations (includes the `vector(768)` column and HNSW index):

```bash
cd backend/SudanDialect.Api
dotnet ef database update
```

7. The API will be available at `http://localhost:5038`. Semantic search endpoint: `GET /api/words/semantic-search?query=...`. Embeddings are backfilled automatically on startup (`Embedding:AutoBackfillOnStartup`); set `Embedding__Enabled=false` to run keyword-only.

### Stop Services

```bash
docker compose down
```

## Tests

### Backend (unit + integration)

Prerequisites:

- Docker Engine is running (integration tests use Testcontainers).

Run all backend tests:

```bash
cd backend
dotnet test --solution SudanDialect.slnx
```

## License

This project is licensed under the GNU General Public License v3.0 
See the LICENSE file for full terms.
