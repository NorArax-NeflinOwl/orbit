namespace Orbit.Web.Services;

/// <summary>
/// Orbit's interface in Polish, keyed by the English it replaces - see <see cref="Translations"/> for
/// why the key is the English text itself.
///
/// Grouped by where the text appears rather than alphabetically, so a page can be checked against the
/// screen it belongs to. Anything not listed here stays in English, which is the fallback by design.
/// </summary>
internal static class PolishTranslations
{
    public static readonly IReadOnlyDictionary<string, string> ByEnglish = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // ---- Navigation and the app shell ----
        ["Dashboard"] = "Pulpit",
        ["Notes"] = "Notatki",
        ["Tasks"] = "Zadania",
        ["Calendar"] = "Kalendarz",
        ["Inventory"] = "Magazyn",
        ["Contacts"] = "Kontakty",
        ["Map"] = "Mapa",
        ["Options"] = "Opcje",
        ["Notifications"] = "Powiadomienia",
        ["Log out"] = "Wyloguj się",
        ["Clear"] = "Wyczyść",
        ["See all notifications"] = "Zobacz wszystkie powiadomienia",
        ["Nothing yet."] = "Na razie nic.",
        ["Loading…"] = "Wczytywanie…",

        // ---- Words the whole app shares ----
        ["Save"] = "Zapisz",
        ["Cancel"] = "Anuluj",
        ["Delete"] = "Usuń",
        ["Edit"] = "Edytuj",
        ["Open"] = "Otwórz",
        ["Add"] = "Dodaj",
        ["Create"] = "Utwórz",
        ["Share"] = "Udostępnij",
        ["Search"] = "Szukaj",
        ["Retry"] = "Spróbuj ponownie",
        ["Remove"] = "Usuń",
        ["Copied"] = "Skopiowano",
        ["Private"] = "Prywatne",
        ["Shared"] = "Udostępnione",
        ["Group"] = "Grupowa",
        ["Pin"] = "Przypnij",
        ["Unpin"] = "Odepnij",
        ["Pinned"] = "Przypięte",
        ["Reply"] = "Odpowiedz",
        ["Forward"] = "Przekaż",
        ["Accept"] = "Akceptuj",
        ["Amount"] = "Ilość",
        ["Min"] = "Min",
        ["Priority"] = "Priorytet",
        ["Low"] = "Niski",
        ["Normal"] = "Zwykły",
        ["High"] = "Wysoki",
        ["Read-only"] = "Tylko do odczytu",
        ["Can share"] = "Może udostępniać",
        ["Can edit"] = "Może edytować",
        ["Can edit, but not share editing"] = "Może edytować, ale nie nadawać edycji",

        // ---- Signing in ----
        ["Log in"] = "Zaloguj się",
        ["Register"] = "Zarejestruj się",
        ["Email or username"] = "E-mail lub nazwa użytkownika",
        ["Password"] = "Hasło",
        ["Repeat password"] = "Powtórz hasło",
        ["Display name"] = "Nazwa wyświetlana",
        ["Email"] = "E-mail",
        ["Username"] = "Nazwa użytkownika",
        ["Don't have an account? "] = "Nie masz konta? ",
        ["Already have an account? "] = "Masz już konto? ",
        ["Invalid email, username, or password."] = "Nieprawidłowy e-mail, nazwa użytkownika lub hasło.",

        // ---- Dashboard ----
        ["Everything on your plate, in one place."] = "Wszystko, co masz na głowie, w jednym miejscu.",
        ["Recent chats"] = "Ostatnie rozmowy",
        ["Groups"] = "Grupy",
        ["Upcoming"] = "Nadchodzące",
        ["Nothing to show."] = "Nie ma nic do pokazania.",
        ["Done"] = "Gotowe",
        ["Admin"] = "Administrator",

        // ---- Notes ----
        ["Add note"] = "Dodaj notatkę",
        ["No notes."] = "Brak notatek.",
        ["Everything you have written down, and anything shared with you."] =
            "Wszystko, co zapisałeś, i to, co ktoś Ci udostępnił.",
        ["Give it a title, or write something in it."] = "Nadaj tytuł albo coś w niej napisz.",
        ["Sharing"] = "Udostępnianie",
        ["Link"] = "Link",

        // ---- Tasks ----
        ["Add task list"] = "Dodaj listę zadań",
        ["Every list you own or someone shared with you."] = "Każda lista, którą masz albo którą Ci udostępniono.",
        ["No task lists yet."] = "Nie ma jeszcze żadnych list zadań.",
        ["No items on this list."] = "Ta lista jest pusta.",
        ["Open checklist"] = "Otwórz listę",
        ["Sort"] = "Sortuj",
        ["Newest first"] = "Od najnowszych",
        ["Oldest first"] = "Od najstarszych",
        ["A to Z"] = "Od A do Z",
        ["Z to A"] = "Od Z do A",
        ["All"] = "Wszystkie",
        ["Not started"] = "Nierozpoczęte",
        ["In progress"] = "W trakcie",
        ["Overdue"] = "Po terminie",
        ["Items"] = "Pozycje",
        ["Group list"] = "Lista grupowa",

        // ---- Calendar ----
        ["Add event"] = "Dodaj wydarzenie",
        ["Your events, and the ones you have been invited to."] = "Twoje wydarzenia i te, na które Cię zaproszono.",
        ["No events."] = "Brak wydarzeń.",
        ["Day"] = "Dzień",
        ["Month"] = "Miesiąc",
        ["Year"] = "Rok",
        ["Today"] = "Dziś",
        ["Yesterday"] = "Wczoraj",
        ["Tasks with a due date"] = "Zadania z terminem",
        ["No tasks with a due date."] = "Brak zadań z terminem.",
        ["Delete event"] = "Usuń wydarzenie",
        ["Guests"] = "Goście",
        ["No guests"] = "Brak gości",
        ["Directions"] = "Wyznacz trasę",
        ["Add to Google Calendar"] = "Dodaj do Kalendarza Google",
        ["The event's end can't be before its start."] = "Koniec wydarzenia nie może być przed jego początkiem.",

        // ---- Inventory ----
        ["Add warehouse"] = "Dodaj magazyn",
        ["Keep separate stocks in separate warehouses, and share one with someone over chat."] =
            "Trzymaj różne zapasy w osobnych magazynach i udostępnij któryś komuś przez czat.",
        ["No warehouses yet. Create one to start tracking stock."] =
            "Nie ma jeszcze magazynów. Utwórz jeden, żeby zacząć pilnować zapasów.",
        ["No items yet."] = "Nie ma jeszcze pozycji.",
        ["Item name"] = "Nazwa pozycji",
        ["Warehouse name"] = "Nazwa magazynu",
        ["Product type"] = "Rodzaj produktu",
        ["Category"] = "Kategoria",
        ["Expires"] = "Termin ważności",
        ["Private - encrypted, and only you can read it"] = "Prywatne — zaszyfrowane, czyta to tylko Ty",
        ["There is no more of this than the minimum you set"] = "Nie zostało tego więcej niż ustawione minimum",
        ["How much of it there is"] = "Ile tego jest",
        ["Restock once the amount reaches this"] = "Uzupełnij, gdy ilość spadnie do tego poziomu",

        // ---- Chat ----
        ["Chats"] = "Rozmowy",
        ["Group chats"] = "Czaty grupowe",
        ["New request"] = "Nowa prośba",
        ["New requests"] = "Nowe prośby",
        ["Open chat"] = "Otwórz rozmowę",
        ["Write a message…"] = "Napisz wiadomość…",
        ["Send"] = "Wyślij",
        ["Replying to"] = "Odpowiadasz na",
        ["Stop replying"] = "Nie odpowiadaj",
        ["Forward to…"] = "Przekaż do…",
        ["No other chats yet."] = "Nie masz jeszcze innych rozmów.",
        ["Message options"] = "Opcje wiadomości",
        ["Allow editing"] = "Pozwól edytować",
        ["They can edit it now."] = "Teraz może to edytować.",
        ["Accepted - added to your account."] = "Przyjęte — dodane do Twojego konta.",
        ["No chats. Search for a user by email address or username to start a conversation."] =
            "Brak rozmów. Wyszukaj kogoś po adresie e-mail lub nazwie użytkownika, żeby zacząć.",
        ["Search for someone and start a conversation."] = "Znajdź kogoś i zacznij rozmowę.",
        ["Email address or username"] = "Adres e-mail lub nazwa użytkownika",

        // ---- Map ----
        ["Share where you are"] = "Udostępnij swoje położenie",
        ["Send once"] = "Wyślij raz",
        ["Keep sharing"] = "Udostępniaj na bieżąco",
        ["Stop"] = "Zatrzymaj",
        ["Forget it"] = "Usuń je",
        ["Orbit isn't allowed to use your location. Turn it on in Options first."] =
            "Orbit nie ma zgody na korzystanie z Twojego położenia. Włącz ją najpierw w Opcjach.",

        // ---- Options ----
        ["Appearance"] = "Wygląd",
        ["Theme"] = "Motyw",
        ["System"] = "Systemowy",
        ["Light"] = "Jasny",
        ["Dark"] = "Ciemny",
        ["Language"] = "Język",
        ["English"] = "angielski",
        ["Polish"] = "polski",
        ["The language Orbit's own interface is written in. Kept on this device."] =
            "Język, w którym napisany jest interfejs Orbita. Zapamiętywany na tym urządzeniu.",
        ["Location"] = "Położenie",
        ["Use my location"] = "Korzystaj z mojego położenia",
        ["Debugger"] = "Debugger",
        ["Mode"] = "Tryb",
        ["Release"] = "Release",
        ["Debug"] = "Debug",
        ["Frontend log level"] = "Poziom logowania frontendu",
        ["Your data"] = "Twoje dane",
        ["Export everything"] = "Wyeksportuj wszystko",
        ["Export"] = "Eksport",
        ["Import"] = "Import",
        ["Danger zone"] = "Strefa niebezpieczna",
        ["Allow notifications"] = "Zezwalaj na powiadomienia",
        ["Allow push"] = "Zezwalaj na push",
        ["Allow email"] = "Zezwalaj na e-mail",
        ["Tell me when something is shared with me"] = "Powiadom mnie, gdy ktoś coś ze mną udostępni",
        ["Keep notifications for (days)"] = "Przechowuj powiadomienia (dni)",
        ["Banner duration (seconds)"] = "Czas wyświetlania dymka (sekundy)",
        ["Banner interval (seconds)"] = "Odstęp między dymkami (sekundy)",
        ["Diagnostics"] = "Diagnostyka",
        ["Show exceptions"] = "Pokazuj wyjątki",

        // ---- Sharing by link ----
        ["Share link"] = "Link do udostępnienia",
        ["Copy link"] = "Kopiuj link",
        ["Stop sharing"] = "Przestań udostępniać",
        ["Ask to edit this"] = "Poproś o możliwość edycji",
        ["Save to my account"] = "Zapisz na moim koncie",
        ["Sign in to save this"] = "Zaloguj się, żeby to zapisać",
        ["This link doesn't work"] = "Ten link nie działa",
        ["Reading needs no account. Saving it to your own does."] =
            "Do czytania konto nie jest potrzebne. Do zapisania u siebie — tak.",
        ["Note"] = "Notatka",
        ["Task list"] = "Lista zadań",
        ["Event"] = "Wydarzenie",
        ["Storage"] = "Magazyn"
    };
}
