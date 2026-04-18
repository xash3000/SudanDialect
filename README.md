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
- **Alphabetical Browsing**: An interactive index allowing users to explore the dictionary letter by letter.

## Tech Stack

- Backend: C# (.NET 10), ASP.NET, Entity Framework Core, PostgreSQL
- Frontend: Angular 21

## Run Locally

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

4. Open `http://localhost:4200`.

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

2. Build the API docker image:

```bash
cd backend
docker build -f SudanDialect.Api/Dockerfile -t ghcr.io/xash3000/sudandialect:latest .
```

3. Start backend services with Docker Compose:

```bash
docker compose up -d
```

4. The API will be available at `http://localhost:5038`.

### Stop Services

```bash
docker compose down
```

## License

This project is licensed under the GNU General Public License v3.0 
See the LICENSE file for full terms.
