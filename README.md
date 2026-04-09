# [sudandialect.com](sudandialect.com)

This project aims to preserve Sudan’s linguistic richness and cultural diversity through a modern digital platform. We believe the Sudanese dialect is more than a way of speaking; it is a record of long-standing cultural and linguistic exchange that deserves to be documented for future generations.

The project began as an effort to connect linguistic heritage with modern technology. Thousands of printed and scanned pages were transformed into a structured digital database, with programmatic data processing to improve search accuracy and make browsing easier.

This dictionary is an ongoing effort, and we welcome contributions and corrections to keep improving the content.


## Tech Stack

- Backend: C#, ASP.NET (.NET 10), Entity Framework Core, PostgreSQL
- Frontend: Angular 21

## Features

- **Advanced Arabic Search**: A robust search engine optimized for the Sudanese dialect, featuring:
  - **Text Normalization**: Automatic handling of arabic letters variants (أ، إ، آ), (ى، ئ), (ة, ه).
  - **Diacritics & Tashkeel Removal**: Seamlessly searches through text regardless of Tashkeel or other character extensions.
  - **Similarity Matching**: Powered by PostgreSQL trigram similarity to handle common spelling variations and typos.
- **Alphabetical Browsing**: An interactive index allowing users to explore the dictionary letter by letter.

## License

This project is licensed under the GNU General Public License v3.0 
See the LICENSE file for full terms.
