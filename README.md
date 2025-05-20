# Jogosítvány Teszt – .NET WebApi Projekt

Ez a webes alkalmazás segít a KRESZ-vizsgára való felkészülésben. A felhasználók regisztrálhatnak, kvízt indíthatnak, a rendszer pedig elmenti az eredményeiket. A kérdések és válaszok SQLite adatbázisból érkeznek, és egyes kérdésekhez kép is tartozik.

## Követelmények

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- A `app.db` adatbázis előre generált, nem kell külön SQL parancsokat futtatni
- A `schema.sql` fájl a teljes adatbázis szerkezetet tartalmazza (opcionálisan újra lefuttatható)
- SQLite Studio ajánlott az adatbázis megtekintéséhez vagy szerkesztéséhez: https://sqlitestudio.pl/

## Indítás

A projekt futtatása parancssorból:

```bash
dotnet run
```

Ezután a weboldal a `https://localhost:5236` címen (vagy amit a konzol kiír) lesz elérhető.

## Használt technológiák

- ASP.NET Core Web API (C#)
- SQLite adatbázis
- jQuery, Bootstrap (frontend)
- SQLite Studio (adatbázis-kezelő)

## Források

A projekt készítése során az alábbi források, példák és dokumentációk segítettek a megvalósításban:

- A kérdésekhez kapcsolódó ikonok és táblaképek az OpenMoji projektből származnak:  
  https://openmoji.org/library/  
  Az ikonok `QuestionImage` táblába lettek beszúrva URL formájában.

- jQuery form beküldés és AJAX példák:  
  https://www.w3schools.com/jquery/jquery_ajax_get_post.asp  
  https://api.jquery.com/jquery.post/

- Modern JavaScript fetch() használat:  
  https://developer.mozilla.org/en-US/docs/Web/API/Fetch_API/Using_Fetch  
  https://www.freecodecamp.org/news/javascript-fetch-api-tutorial-with-js-fetch-post-and-header-examples/

- Bootstrap űrlapok, gombok és komponensek:  
  https://getbootstrap.com/docs/5.3/forms/overview/  
  https://getbootstrap.com/docs/5.3/components/buttons/  
  https://getbootstrap.com/docs/5.3/components/alerts/

- SQLite alapok és lekérdezések:  
  https://www.sqlitetutorial.net

- ASP.NET session kezelés, cookie példa:  
  https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.httpresponse.cookies  
  https://stackoverflow.com/questions/76906864/asp-net-core-cant-append-cookies

- Jelszó hash + salt logika inspiráció:  
  https://stackoverflow.com/questions/4181198/how-to-hash-a-password

- További gyakorlati segítséget Stack Overflow válaszok, GitHub példák és fejlesztői fórumok nyújtottak, ahol hasonló hibákat és megoldásokat találtam.