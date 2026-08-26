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
        // Same word in Polish - written down rather than left out, so the coverage test can tell a
        // deliberate match from an entry somebody forgot.
        ["Push"] = "Push",
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
        ["There is less of this than the minimum you set"] = "Zostało tego mniej niż ustawione minimum",
        ["How much of it there is"] = "Ile tego jest",
        ["Restock once the amount drops below this"] = "Uzupełnij, gdy ilość spadnie poniżej tego poziomu",

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

        // ---- The rest of the interface: page titles, form labels, and the longer explanations
        // that sit under a setting or beside an empty list. ----
        ["Orbit — Dashboard"] = "Orbit — Pulpit",
        ["Orbit — Notes"] = "Orbit — Notatki",
        ["Orbit — Tasks"] = "Orbit — Zadania",
        ["Orbit — Calendar"] = "Orbit — Kalendarz",
        ["Orbit — Inventory"] = "Orbit — Magazyn",
        ["Orbit — Contacts"] = "Orbit — Kontakty",
        ["Orbit — Chat"] = "Orbit — Rozmowy",
        ["Orbit — Map"] = "Orbit — Mapa",
        ["Orbit — Options"] = "Orbit — Opcje",
        ["Orbit — Notifications"] = "Orbit — Powiadomienia",
        ["Orbit — Log in"] = "Orbit — Logowanie",
        ["Orbit — Register"] = "Orbit — Rejestracja",
        ["Title"] = "Tytuł",
        ["Name"] = "Nazwa",
        ["Content"] = "Treść",
        ["Description"] = "Opis",
        ["Color"] = "Kolor",
        ["Start"] = "Początek",
        ["End"] = "Koniec",
        ["Back"] = "Wróć",
        ["Confirm"] = "Potwierdź",
        ["Copy"] = "Kopiuj",
        ["None"] = "Brak",
        ["Every"] = "Co",
        ["Custom"] = "Własne",
        ["Completed"] = "Ukończone",
        ["Connected"] = "Połączone",
        ["Verified"] = "Potwierdzony",
        ["Not verified"] = "Niepotwierdzony",
        ["Cleared"] = "Wyczyszczone",
        ["All day"] = "Cały dzień",
        ["Shared by"] = "Udostępnił",
        ["Messages"] = "Wiadomości",
        ["Conversations"] = "Rozmowy",
        ["Daily"] = "Codziennie",
        ["Weekly"] = "Co tydzień",
        ["Monthly"] = "Co miesiąc",
        ["Frequency"] = "Częstotliwość",
        ["Recurring event"] = "Wydarzenie cykliczne",
        ["Repeat until (optional)"] = "Powtarzaj do (opcjonalnie)",
        ["Repeats:"] = "Powtarza się:",
        ["All-day event"] = "Wydarzenie całodniowe",
        ["Add reminder"] = "Dodaj przypomnienie",
        ["Reminders"] = "Przypomnienia",
        ["No reminders."] = "Brak przypomnień.",
        ["Add a guest from contacts"] = "Dodaj gościa z kontaktów",
        ["No guests."] = "Brak gości.",
        ["Guests:"] = "Goście:",
        ["Location:"] = "Miejsce:",
        ["Remove location"] = "Usuń miejsce",
        ["Pick a point on the map to set an address"] = "Wskaż punkt na mapie, aby ustawić adres",
        ["Show tasks in year view"] = "Pokaż zadania w widoku roku",
        ["Notification when the event is created"] = "Powiadomienie przy utworzeniu wydarzenia",
        ["Notification as the event approaches"] = "Powiadomienie przed wydarzeniem",
        ["This event was shared by"] = "To wydarzenie udostępnił",
        ["Checklist item"] = "Pozycja listy",
        ["This note was shared by"] = "Tę notatkę udostępnił",
        ["Add item"] = "Dodaj pozycję",
        ["Item description"] = "Opis pozycji",
        ["Link to list"] = "Powiąż z listą",
        ["Move to list"] = "Przenieś do listy",
        ["Remind daily"] = "Przypominaj codziennie",
        ["Daily reminder channel"] = "Kanał codziennego przypomnienia",
        ["Daily reminder time"] = "Godzina codziennego przypomnienia",
        ["Overdue notification"] = "Powiadomienie po terminie",
        ["No items."] = "Brak pozycji.",
        ["Back to task lists"] = "Wróć do list zadań",
        ["This task list no longer exists."] = "Ta lista zadań już nie istnieje.",
        ["This task list was shared by"] = "Tę listę zadań udostępnił",
        ["Completed - follows the linked list, can't be set by hand"] =
            "Ukończone — wynika z powiązanej listy, nie da się ustawić ręcznie",
        ["Completion follows the linked list, so it can't be ticked by hand."] =
            "Ukończenie wynika z powiązanej listy, więc nie da się go odhaczyć ręcznie.",
        ["Sorts this list against the others on the Tasks page. Where it has got to is worked out from its items."] =
            "Porządkuje tę listę względem pozostałych na stronie Zadania. To, na jakim jest etapie, wynika z jej pozycji.",
        ["All warehouses"] = "Wszystkie magazyny",
        ["Expiry notification"] = "Powiadomienie o terminie ważności",
        ["Running low"] = "Kończy się",
        ["Everything in this warehouse, edited together - a low item still raises a restock task."] =
            "Cała zawartość magazynu, edytowana razem — kończąca się pozycja nadal tworzy zadanie uzupełnienia.",
        ["No contacts to share with yet - start a conversation first."] =
            "Nie masz jeszcze komu udostępnić — zacznij od rozmowy.",
        ["Allow chatting"] = "Zezwól na rozmowę",
        ["End-to-end encrypted conversations."] = "Rozmowy szyfrowane end-to-end.",
        ["Jump to newest message"] = "Przejdź do najnowszej wiadomości",
        ["Write to the group"] = "Napisz do grupy",
        ["New group"] = "Nowa grupa",
        ["Group name"] = "Nazwa grupy",
        ["Your groups"] = "Twoje grupy",
        ["No groups yet."] = "Nie masz jeszcze grup.",
        ["Who's in it"] = "Kto należy",
        ["Weekend trip"] = "Wyjazd na weekend",
        ["You need at least one contact to start a group."] =
            "Aby założyć grupę, potrzebujesz co najmniej jednego kontaktu.",
        ["Chats with more than one other person, encrypted the same way one-to-one chats are."] =
            "Rozmowy z więcej niż jedną osobą, szyfrowane tak samo jak rozmowy jeden na jeden.",
        ["Nothing here yet. Anything sent before you joined stays unreadable - no copy of it was ever encrypted for you."] =
            "Na razie nic tu nie ma. Wiadomości sprzed Twojego dołączenia pozostaną nieczytelne — nigdy nie zaszyfrowano ich dla Ciebie.",
        ["No contacts. A contact will appear here once you start a chat with someone."] =
            "Brak kontaktów. Kontakt pojawi się tutaj, gdy zaczniesz z kimś rozmowę.",
        ["Unlock"] = "Odblokuj",
        ["Unlock chat"] = "Odblokuj rozmowy",
        ["Set password"] = "Ustaw hasło",
        ["Set a password to use chat"] = "Ustaw hasło, aby korzystać z rozmów",
        ["I forgot my password"] = "Nie pamiętam hasła",
        ["Reset your password"] = "Zresetuj hasło",
        ["Send code"] = "Wyślij kod",
        ["Send again"] = "Wyślij ponownie",
        ["Code from the email"] = "Kod z e-maila",
        ["We'll email a code to"] = "Wyślemy kod na adres",
        ["The code goes to"] = "Kod trafi na adres",
        ["Code sent to"] = "Kod wysłany na",
        ["New password"] = "Nowe hasło",
        ["Repeat new password"] = "Powtórz nowe hasło",
        ["Current password"] = "Obecne hasło",
        ["Nobody can recover it for you: forget it and your chat history becomes unreadable."] =
            "Nikt go za Ciebie nie odzyska: jeśli je zapomnisz, historia rozmów stanie się nieczytelna.",
        ["Resetting needs a confirmed email address, and yours isn't confirmed yet. Confirm it in"] =
            "Reset wymaga potwierdzonego adresu e-mail, a Twój nie jest jeszcze potwierdzony. Potwierdź go w sekcji ",
        ["The one location you've recorded for yourself, and where it is."] =
            "Jedyne położenie, jakie dla siebie zapisałeś, i gdzie ono jest.",
        ["Nothing in it yet."] = "Na razie nic w tym nie ma.",
        ["You'll get your own read-only copy. Nothing you do changes theirs."] =
            "Dostaniesz własną kopię tylko do odczytu. Nic, co zrobisz, nie zmieni oryginału.",
        ["Frontend error"] = "Błąd interfejsu",
        ["Account"] = "Konto",
        ["Your account, appearance, and notification preferences."] = "Twoje konto, wygląd i ustawienia powiadomień.",
        ["Your profile, sign-in address, and password."] = "Twój profil, adres logowania i hasło.",
        ["Save profile"] = "Zapisz profil",
        ["Change password"] = "Zmień hasło",
        ["Email verification"] = "Potwierdzenie adresu e-mail",
        ["Delete account"] = "Usuń konto",
        ["Disconnect"] = "Odłącz",
        ["Push notifications"] = "Powiadomienia push",
        ["Allow mobile notification"] = "Zezwalaj na dymek powiadomienia",
        ["Not available yet"] = "Jeszcze niedostępne",
        ["Not supported in this browser"] = "Nieobsługiwane w tej przeglądarce",
        ["Only shown in non-production environments."] = "Widoczne tylko poza środowiskiem produkcyjnym.",
        ["Choose how Orbit looks on this device."] = "Wybierz, jak Orbit wygląda na tym urządzeniu.",
        ["Deleting your account is permanent and cannot be undone."] = "Usunięcie konta jest trwałe i nieodwracalne.",
        ["How long a notification banner stays on screen (1-30)."] =
            "Jak długo dymek powiadomienia zostaje na ekranie (1–30).",
        ["Minimum quiet time before the next banner may appear (1-300)."] =
            "Minimalna przerwa, zanim pojawi się kolejny dymek (1–300).",
        ["How long a notification stays on the"] = "Jak długo powiadomienie zostaje na",
        ["What this browser reports about Orbit itself. Kept on this device."] =
            "Co ta przeglądarka raportuje o samym Orbicie. Zapamiętywane na tym urządzeniu.",
        ["Kept on this device - a browser grants location per device, so the answer is given per device too."] =
            "Zapamiętywane na tym urządzeniu — przeglądarka przyznaje dostęp do położenia osobno na każdym, więc i odpowiedź jest osobna.",
        ["Everything you own — notes, task lists, events and storages — as one JSON file, and back again."] =
            "Wszystko, co masz — notatki, listy zadań, wydarzenia i magazyny — w jednym pliku JSON i z powrotem.",
        ["Master switch for the three settings below and for anything showing up in the Notifications panel/badge/banner at all."] =
            "Główny przełącznik dla trzech ustawień poniżej i dla wszystkiego, co w ogóle pojawia się w panelu powiadomień.",
        ["Instant alerts for new messages, event reminders and overdue tasks, even while Orbit isn't open."] =
            "Natychmiastowe alerty o nowych wiadomościach, przypomnieniach i zaległych zadaniach, nawet gdy Orbit jest zamknięty.",
        ["Shows a short banner at the top of the page when a new notification arrives while Orbit is open."] =
            "Pokazuje krótki dymek na górze strony, gdy nadejdzie powiadomienie przy otwartym Orbicie.",
        ["Lets any calendar event, task, or inventory item's own email setting actually send an email."] =
            "Sprawia, że ustawienie e-maila przy wydarzeniu, zadaniu czy pozycji magazynu faktycznie wysyła e-mail.",
        ["Lets any calendar event, task, or inventory item's own push setting actually send a push notification."] =
            "Sprawia, że ustawienie push przy wydarzeniu, zadaniu czy pozycji magazynu faktycznie wysyła powiadomienie.",
        ["A verified address is what a password reset is sent to, so it has to be one you can read."] =
            "Na potwierdzony adres wysyłamy reset hasła, więc musi to być adres, do którego masz dostęp.",
        ["Changing it also re-encrypts your chat key backup, so your messages stay readable when you next sign in elsewhere."] =
            "Zmiana szyfruje na nowo kopię klucza rozmów, dzięki czemu wiadomości pozostaną czytelne po zalogowaniu się gdzie indziej.",
        ["Your account has no password yet, so Google is currently the only way in - set one below before disconnecting."] =
            "Twoje konto nie ma jeszcze hasła, więc Google jest jedyną drogą do środka — ustaw hasło poniżej przed odłączeniem.",
        ["Collapse sidebar"] = "Zwiń panel boczny",

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
        ["Storage"] = "Magazyn",
        ["Warehouse"] = "Magazyn",
        ["Checklist"] = "Lista kontrolna",

        // ---- What the app says when something worked, or didn't. Composed in code rather than
        // markup, but shown on screen just the same - unlike an exception message, which stays
        // English because it is written for whoever is reading the code or the logs. ----
        ["Couldn't load chat. Check your connection and try again."] =
            "Nie udało się wczytać rozmów. Sprawdź połączenie i spróbuj ponownie.",
        ["Couldn't load this conversation. Check your connection and try again."] =
            "Nie udało się wczytać tej rozmowy. Sprawdź połączenie i spróbuj ponownie.",
        ["Couldn't load the dashboard. Check your connection and try again."] =
            "Nie udało się wczytać pulpitu. Sprawdź połączenie i spróbuj ponownie.",
        ["Couldn't load your groups. Check your connection and try again."] =
            "Nie udało się wczytać grup. Sprawdź połączenie i spróbuj ponownie.",
        ["Couldn't load your warehouses. Check your connection and try again."] =
            "Nie udało się wczytać magazynów. Sprawdź połączenie i spróbuj ponownie.",
        ["Failed to load contacts. Check your connection and try again."] =
            "Nie udało się wczytać kontaktów. Sprawdź połączenie i spróbuj ponownie.",
        ["Failed to connect to the server. Check your connection and try again."] =
            "Nie udało się połączyć z serwerem. Sprawdź połączenie i spróbuj ponownie.",
        ["Couldn't load your account. Reload the page and try again."] =
            "Nie udało się wczytać konta. Odśwież stronę i spróbuj ponownie.",
        ["Couldn't load this task list. Try again."] = "Nie udało się wczytać tej listy zadań. Spróbuj ponownie.",
        ["Failed to load notification settings. Try reloading the page."] =
            "Nie udało się wczytać ustawień powiadomień. Spróbuj odświeżyć stronę.",
        ["Failed to save the event. Try again."] = "Nie udało się zapisać wydarzenia. Spróbuj ponownie.",
        ["Failed to save the note. Try again."] = "Nie udało się zapisać notatki. Spróbuj ponownie.",
        ["Failed to save the task list. Try again."] = "Nie udało się zapisać listy zadań. Spróbuj ponownie.",
        ["Failed to save the warehouse. Try again."] = "Nie udało się zapisać magazynu. Spróbuj ponownie.",
        ["Couldn't save that change. Try again."] = "Nie udało się zapisać tej zmiany. Spróbuj ponownie.",
        ["Couldn't save your profile. Try again."] = "Nie udało się zapisać profilu. Spróbuj ponownie.",
        ["Couldn't save it. Try again."] = "Nie udało się tego zapisać. Spróbuj ponownie.",
        ["Couldn't delete the event. Try again."] = "Nie udało się usunąć wydarzenia. Spróbuj ponownie.",
        ["Failed to create the warehouse. Try again."] = "Nie udało się utworzyć magazynu. Spróbuj ponownie.",
        ["Failed to delete the warehouse. Try again."] = "Nie udało się usunąć magazynu. Spróbuj ponownie.",
        ["Failed to move this item. Try again."] = "Nie udało się przenieść tej pozycji. Spróbuj ponownie.",
        ["Failed to share the note."] = "Nie udało się udostępnić notatki.",
        ["Failed to share the task list."] = "Nie udało się udostępnić listy zadań.",
        ["Failed to share the warehouse."] = "Nie udało się udostępnić magazynu.",
        ["Couldn't forward the message."] = "Nie udało się przekazać wiadomości.",
        ["Couldn't create the link. Try again."] = "Nie udało się utworzyć linku. Spróbuj ponownie.",
        ["Couldn't stop sharing. Try again."] = "Nie udało się zakończyć udostępniania. Spróbuj ponownie.",
        ["This can't be shared with a link. Private items stay private."] =
            "Tego nie da się udostępnić linkiem. Elementy prywatne pozostają prywatne.",
        ["Couldn't send the request. Try again."] = "Nie udało się wysłać prośby. Spróbuj ponownie.",
        ["They haven't set up encryption yet, so they can't receive this."] =
            "Ta osoba nie skonfigurowała jeszcze szyfrowania, więc tego nie odbierze.",
        ["{0} is currently editing this event - try again in a moment."] =
            "{0} właśnie edytuje to wydarzenie — spróbuj za chwilę.",
        ["{0} is currently editing this note - try again in a moment."] =
            "{0} właśnie edytuje tę notatkę — spróbuj za chwilę.",
        ["{0} is currently editing this task list - try again in a moment."] =
            "{0} właśnie edytuje tę listę zadań — spróbuj za chwilę.",
        ["Enter a password."] = "Wpisz hasło.",
        ["The two passwords don't match."] = "Hasła nie są takie same.",
        ["The two new passwords don't match."] = "Nowe hasła nie są takie same.",
        ["That password isn't right."] = "To hasło jest nieprawidłowe.",
        ["That current password isn't right."] = "Obecne hasło jest nieprawidłowe.",
        ["This account already has a password - enter it instead."] =
            "To konto ma już hasło — wpisz je zamiast ustawiać nowe.",
        ["Password changed."] = "Hasło zmienione.",
        ["Enter your password to confirm."] = "Wpisz hasło, aby potwierdzić.",
        ["Couldn't change your password. Try again."] = "Nie udało się zmienić hasła. Spróbuj ponownie.",
        ["That code isn't valid any more. Request a new one."] = "Ten kod stracił ważność. Poproś o nowy.",
        ["Couldn't send the code. Try again."] = "Nie udało się wysłać kodu. Spróbuj ponownie.",
        ["Couldn't confirm the code. Try again."] = "Nie udało się potwierdzić kodu. Spróbuj ponownie.",
        ["Couldn't delete your account. Try again."] = "Nie udało się usunąć konta. Spróbuj ponownie.",
        ["Couldn't sign in with Google. Try again."] = "Nie udało się zalogować przez Google. Spróbuj ponownie.",
        ["Couldn't connect Google. Try again."] = "Nie udało się połączyć z Google. Spróbuj ponownie.",
        ["Couldn't disconnect Google. Try again."] = "Nie udało się odłączyć Google. Spróbuj ponownie.",
        ["No user found with that email address or username."] =
            "Nie znaleziono użytkownika o takim adresie e-mail ani nazwie.",
        ["Failed to update notification settings. Try again."] =
            "Nie udało się zapisać ustawień powiadomień. Spróbuj ponownie.",
        ["Failed to enable push notifications. Check your browser permissions and try again."] =
            "Nie udało się włączyć powiadomień push. Sprawdź uprawnienia przeglądarki i spróbuj ponownie.",
        ["Failed to update the push notification subscription. Try again."] =
            "Nie udało się zaktualizować subskrypcji powiadomień push. Spróbuj ponownie.",
        ["Location recorded."] = "Położenie zapisane.",
        ["Location forgotten."] = "Położenie usunięte.",
        ["Couldn't save your location. Try again."] = "Nie udało się zapisać położenia. Spróbuj ponownie.",
        ["Couldn't load your location. Try again."] = "Nie udało się wczytać położenia. Spróbuj ponownie.",
        ["Couldn't clear your location. Try again."] = "Nie udało się usunąć położenia. Spróbuj ponownie.",
        ["Couldn't share your location. Try again."] = "Nie udało się udostępnić położenia. Spróbuj ponownie.",
        ["Record where you are first - there is nothing to share yet."] =
            "Najpierw zapisz, gdzie jesteś — na razie nie ma czego udostępnić.",
        ["Stopped. Orbit no longer holds that position at all."] = "Zatrzymano. Orbit nie przechowuje już tego położenia.",
        ["The map couldn't be drawn, but your location is recorded."] =
            "Nie udało się narysować mapy, ale Twoje położenie zostało zapisane.",
        ["Your location couldn't be read in this browser."] = "Nie udało się odczytać położenia w tej przeglądarce.",
        ["Couldn't build the export. Try again."] = "Nie udało się przygotować eksportu. Spróbuj ponownie.",
        ["Couldn't import that file. Try again."] = "Nie udało się zaimportować tego pliku. Spróbuj ponownie.",
        ["That file didn't contain an Orbit export."] = "Ten plik nie zawiera eksportu z Orbita.",
        ["Orbit couldn't read that file. It may have been written by a different version."] =
            "Orbit nie potrafi odczytać tego pliku. Mógł go zapisać inna wersja aplikacji.",
        ["Exported {0} notes, {1} task lists, {2} events and {3} storages."] =
            "Wyeksportowano: notatki ({0}), listy zadań ({1}), wydarzenia ({2}), magazyny ({3}).",
        ["Imported {0} notes, {1} task lists, {2} events and {3} storages."] =
            "Zaimportowano: notatki ({0}), listy zadań ({1}), wydarzenia ({2}), magazyny ({3}).",
        ["That didn't work. Reload the group and try again."] = "Nie udało się. Odśwież grupę i spróbuj ponownie.",
        ["That message is no longer yours to delete."] = "Tej wiadomości nie możesz już usunąć.",
        ["Something went wrong. Try again."] = "Coś poszło nie tak. Spróbuj ponownie.",
        ["{0} is currently editing this note - you can't edit it right now."] =
            "{0} właśnie edytuje tę notatkę — w tej chwili nie możesz jej zmieniać.",
        ["{0} is currently editing this task list - you can't edit it right now."] =
            "{0} właśnie edytuje tę listę zadań — w tej chwili nie możesz jej zmieniać.",
        ["{0} is currently editing this event - you can't edit it right now."] =
            "{0} właśnie edytuje to wydarzenie — w tej chwili nie możesz go zmieniać.",
        ["{0} is currently editing this warehouse - you can't edit it right now."] =
            "{0} właśnie edytuje ten magazyn — w tej chwili nie możesz go zmieniać.",
        ["{0} is currently editing \"{1}\" - try again in a moment."] =
            "{0} właśnie edytuje „{1}” — spróbuj za chwilę.",

        // ---- Fragments that begin lower-case, dropdown placeholders, and the longer
        // explanations under a setting. The first sweep matched only capitalised text, which is
        // how a whole class of these survived it. ----
        ["or"] = "lub",
        ["On"] = "Wł.",
        ["before"] = "przed",
        ["minutes"] = "minut",
        ["hours"] = "godzin",
        ["days"] = "dni",
        ["weeks"] = "tygodni",
        ["daily"] = "codziennie",
        ["weekly"] = "co tydzień",
        ["monthly"] = "co miesiąc",
        ["· Edited"] = "· Edytowano",
        ["follows another list"] = "wynika z innej listy",
        ["notifications page"] = "stronie powiadomień",
        ["first, then come back here."] = "najpierw tam, a potem wróć tutaj.",
        ["tasks due today"] = "zadań na dziś",
        ["events today"] = "wydarzeń dziś",
        ["new chat requests"] = "nowych próśb o rozmowę",
        ["-- select a contact --"] = "— wybierz kontakt —",
        ["Pick a contact…"] = "Wybierz kontakt…",
        ["Add someone…"] = "Dodaj osobę…",
        ["Move to…"] = "Przenieś do…",
        ["a note"] = "notatkę",
        ["a task list"] = "listę zadań",
        ["an event"] = "wydarzenie",
        ["a warehouse"] = "magazyn",
        ["An error occurred while logging in. Try again."] = "Wystąpił błąd podczas logowania. Spróbuj ponownie.",
        ["An error occurred while registering. Try again."] = "Wystąpił błąd podczas rejestracji. Spróbuj ponownie.",
        ["That email or username is already taken."] = "Ten e-mail lub nazwa użytkownika są już zajęte.",
        ["Tick items off; use Edit to change the list itself."] = "Odhaczaj pozycje; użyj Edytuj, aby zmienić samą listę.",
        ["The checklist view of a group list also shows every list its items link to, so the whole group can be worked through on one screen."] =
            "Widok listy grupowej pokazuje też każdą listę, z którą powiązane są jej pozycje, więc całą grupę można przejść na jednym ekranie.",
        ["Encrypted in this browser before it is saved, so Orbit's servers hold something they can't read. A private note can't be shared, and any share of it stops working while it stays private. Losing your password means losing it - a reset replaces the key it was encrypted with, exactly as it does for chat history."] =
            "Szyfrowana w tej przeglądarce przed zapisem, więc serwery Orbita trzymają coś, czego nie potrafią odczytać. Prywatnej notatki nie da się udostępnić, a każde jej udostępnienie przestaje działać, dopóki pozostaje prywatna. Utrata hasła oznacza utratę notatki — reset podmienia klucz, którym ją zaszyfrowano, dokładnie tak jak przy historii rozmów.",
        ["Encrypted in this browser before it is saved, so Orbit's servers hold something they can't read. A private list can't be shared, and any share of it stops working while it stays private. Because the server can no longer read a due date, a private list gets no overdue or daily reminders - and losing your password loses the list, exactly as a reset loses chat history."] =
            "Szyfrowana w tej przeglądarce przed zapisem, więc serwery Orbita trzymają coś, czego nie potrafią odczytać. Prywatnej listy nie da się udostępnić, a każde jej udostępnienie przestaje działać, dopóki pozostaje prywatna. Ponieważ serwer nie odczyta już terminu, prywatna lista nie dostaje przypomnień o zaległościach ani codziennych — a utrata hasła oznacza utratę listy, tak samo jak reset oznacza utratę historii rozmów.",
        ["Encrypted in this browser before it is saved, so Orbit's servers hold something they can't read - no item rows for this warehouse exist there at all. A private warehouse can't be shared, and any share of it stops working while it stays private. Because the server can no longer read a quantity or an expiry date, it raises no restock tasks and sends no expiry reminders. Losing your password means losing it."] =
            "Szyfrowany w tej przeglądarce przed zapisem, więc serwery Orbita trzymają coś, czego nie potrafią odczytać — nie ma tam w ogóle wierszy z pozycjami tego magazynu. Prywatnego magazynu nie da się udostępnić, a każde jego udostępnienie przestaje działać, dopóki pozostaje prywatny. Ponieważ serwer nie odczyta już ilości ani terminu ważności, nie tworzy zadań uzupełnienia i nie wysyła przypomnień o terminach. Utrata hasła oznacza utratę magazynu.",
        ["Chat is end-to-end encrypted, and your key is protected with a password. Your account signs in with Google and doesn't have one yet, so pick one now - it's also what lets you read your messages on another device."] =
            "Rozmowy są szyfrowane end-to-end, a Twój klucz chroni hasło. Twoje konto loguje się przez Google i nie ma jeszcze hasła, więc ustaw je teraz — to ono pozwala też czytać wiadomości na innym urządzeniu.",
        ["This browser doesn't have a copy of your encryption key yet. Enter your password to restore it from your encrypted backup - it never leaves this browser."] =
            "Ta przeglądarka nie ma jeszcze kopii Twojego klucza szyfrującego. Wpisz hasło, aby odtworzyć go z zaszyfrowanej kopii — klucz nigdy nie opuszcza tej przeglądarki.",
        ["Setting a new password starts your chat over: messages encrypted under the old one stay unreadable, because Orbit's servers never had the key to them."] =
            "Ustawienie nowego hasła zaczyna rozmowy od nowa: wiadomości zaszyfrowane starym pozostaną nieczytelne, bo serwery Orbita nigdy nie miały do nich klucza.",
        ["Chat requires a secure connection (an address starting with \"https://\", or \"http://localhost\"). Open Orbit at such an address and try again."] =
            "Rozmowy wymagają bezpiecznego połączenia (adres zaczynający się od „https://” albo „http://localhost”). Otwórz Orbita pod takim adresem i spróbuj ponownie.",
        ["Nothing recorded yet. Recording asks your browser for your position - it will ask your permission first, and nothing is read until you press the button. Orbit keeps one point and no history: recording again replaces it, and forgetting it leaves nothing behind."] =
            "Nic jeszcze nie zapisano. Zapis pyta przeglądarkę o Twoje położenie — najpierw poprosi Cię o zgodę, a nic nie zostanie odczytane, dopóki nie naciśniesz przycisku. Orbit trzyma jeden punkt i żadnej historii: kolejny zapis go zastępuje, a usunięcie nie zostawia niczego.",
        ["Your position goes out again every minute while this page is open, and stops the moment it isn't. Ending a share deletes the position from Orbit - nothing is kept afterwards."] =
            "Twoje położenie wysyłane jest ponownie co minutę, dopóki ta strona jest otwarta, i ustaje w chwili jej zamknięcia. Zakończenie udostępniania usuwa położenie z Orbita — nic nie zostaje.",
        ["Anyone with this link can read it without an account. They can't change it, and they can save their own read-only copy by signing in."] =
            "Każdy, kto ma ten link, może to przeczytać bez konta. Nie może tego zmienić, a po zalogowaniu może zapisać u siebie własną kopię tylko do odczytu.",
        ["It may have been turned off by whoever shared it, or the thing it pointed at may be gone. Ask them for a new one."] =
            "Osoba, która go udostępniła, mogła go wyłączyć, albo rzecz, na którą wskazywał, już nie istnieje. Poproś o nowy.",
        ["A note, task list, event, warehouse, or someone's location. The invitation always appears in your notifications either way - this adds a push notification or email on top, so you hear about it straight away rather than next time you look."] =
            "Notatka, lista zadań, wydarzenie, magazyn albo czyjeś położenie. Zaproszenie i tak zawsze pojawia się w powiadomieniach — to dokłada do tego powiadomienie push lub e-mail, żebyś dowiedział się od razu, a nie przy następnym zaglądnięciu.",
        ["Downloads a file holding everything in your account. Things other people shared with you stay theirs and are left out. A private item travels sealed: import it back here and it opens again, import it into another account and nobody there can read it."] =
            "Pobiera plik ze wszystkim, co masz na koncie. Rzeczy udostępnione Ci przez innych pozostają ich i nie trafiają do pliku. Element prywatny podróżuje zaszyfrowany: zaimportowany tutaj z powrotem znów się otworzy, a zaimportowany na inne konto pozostanie tam nieczytelny.",
        ["Adds everything in a file to this account. Nothing is replaced or matched up with what you already have, so importing the same file twice leaves two copies of everything."] =
            "Dodaje do tego konta wszystko z pliku. Nic nie jest zastępowane ani dopasowywane do tego, co już masz, więc dwukrotny import tego samego pliku zostawia dwie kopie wszystkiego.",
        ["Lets the Notifications panel list this browser's own recent errors, each with a \"Copy\" button for reporting a bug."] =
            "Pozwala panelowi powiadomień wypisać ostatnie błędy tej przeglądarki, każdy z przyciskiem „Kopiuj” do zgłoszenia usterki.",
        ["Lets Orbit ask this browser where you are, for the map and for sharing your position with someone. Until you turn this on, nothing asks - not even the browser's own permission prompt. Turning it on doesn't send anything anywhere by itself."] =
            "Pozwala Orbitowi zapytać tę przeglądarkę, gdzie jesteś — na potrzeby mapy i udostępniania położenia. Dopóki tego nie włączysz, nic nie pyta — nawet własne okno zgody przeglądarki. Samo włączenie niczego nigdzie nie wysyła.",
        ["Debug shows what the app can tell you about itself - the captured log, and detail behind an error rather than just \"something went wrong\". Release keeps it out of the way. This is a choice about what you are shown, not the build Orbit was compiled as, which was fixed long before this page opened."] =
            "Debug pokazuje, co aplikacja potrafi powiedzieć o sobie — zapisany log i szczegóły błędu zamiast samego „coś poszło nie tak”. Release trzyma to poza zasięgiem wzroku. To wybór dotyczący tego, co widzisz, a nie tego, jak Orbit został skompilowany — to ustalono na długo przed otwarciem tej strony.",
        ["The least severe line this browser keeps in its own log. Warning by default: anything lower fills the buffer with routine noise long before an actual failure needs the space. Takes effect straight away."] =
            "Najłagodniejszy wpis, jaki ta przeglądarka zachowuje we własnym logu. Domyślnie Warning: cokolwiek niżej zapełnia bufor rutynowym szumem, zanim miejsce przyda się przy prawdziwej awarii. Działa od razu.",
        ["before it is deleted for good (1-90). Clearing the panel only tidies them out of the way; this is what actually removes them."] =
            "zanim zostanie usunięte na dobre (1–90). Wyczyszczenie panelu tylko je uprząta; to ustawienie faktycznie je kasuje.",
        ["Permanently deletes your account and everything in it - notes, tasks, calendar events, inventory, and chat history. This cannot be undone."] =
            "Trwale usuwa Twoje konto i wszystko, co się w nim znajduje — notatki, zadania, wydarzenia, magazyn i historię rozmów. Tego nie da się cofnąć.",
        ["Saving a different address doesn't move the account to it on its own - that only happens once you confirm the code sent to the new address."] =
            "Zapisanie innego adresu samo w sobie nie przenosi na niego konta — dzieje się to dopiero po potwierdzeniu kodu wysłanego na nowy adres.",
        ["With a confirmed email address or a connected Google account, Orbit offers links that hand something off to Google: adding a calendar event or a task's deadline to Google Calendar, opening an address in Google Maps, and getting directions to it. The links open Google with everything filled in - Orbit never writes to your calendar itself and asks for no access to it."] =
            "Przy potwierdzonym adresie e-mail lub połączonym koncie Google Orbit oferuje linki przekazujące coś do Google: dodanie wydarzenia albo terminu zadania do Kalendarza Google, otwarcie adresu w Mapach Google i wyznaczenie do niego trasy. Linki otwierają Google z wypełnionymi danymi — Orbit sam nigdy nie zapisuje nic w Twoim kalendarzu i nie prosi o dostęp do niego.",

        // Composed in code: option labels, confirmations, and the stand-ins shown when
        // something can't be read or named.
        ["5 minutes before"] = "5 minut przed",
        ["10 minutes before"] = "10 minut przed",
        ["15 minutes before"] = "15 minut przed",
        ["30 minutes before"] = "30 minut przed",
        ["1 hour before"] = "godzinę przed",
        ["2 hours before"] = "2 godziny przed",
        ["12 hours before"] = "12 godzin przed",
        ["1 day before"] = "dzień przed",
        ["2 days before"] = "2 dni przed",
        ["1 week before"] = "tydzień przed",
        ["Email and push"] = "E-mail i push",
        ["you can edit it"] = "możesz to edytować",
        ["you can edit it, but not share it further with editing"] = "możesz to edytować, ale nie udostępniać dalej z prawem edycji",
        ["you can share it further, but not edit it"] = "możesz to udostępniać dalej, ale nie edytować",
        ["you can view it"] = "możesz to przeglądać",
        ["Delete this event? This can't be undone."] = "Usunąć to wydarzenie? Tego nie da się cofnąć.",
        ["Delete your account? This permanently deletes everything - notes, tasks, calendar events, inventory, and chat history. This cannot be undone."] = "Usunąć konto? To trwale usuwa wszystko — notatki, zadania, wydarzenia, magazyn i historię rozmów. Tego nie da się cofnąć.",
        ["Delete \"{0}\" and everything in it?"] = "Usunąć „{0}” razem z całą zawartością?",
        ["Profile saved."] = "Profil zapisany.",
        ["Email address confirmed."] = "Adres e-mail potwierdzony.",
        ["Google connected."] = "Google połączone.",
        ["Google disconnected."] = "Google odłączone.",
        ["Already shared with that contact - sent a reminder."] = "Już udostępnione tej osobie — wysłano przypomnienie.",
        ["Shared - they'll see it in your chat."] = "Udostępnione — zobaczy to w rozmowie z Tobą.",
        ["Sharing. Your position goes out again every minute while this page is open."] = "Udostępniasz. Twoje położenie wysyłane jest co minutę, dopóki ta strona jest otwarta.",
        ["Sent. That one position is all they can see."] = "Wysłano. To jedno położenie to wszystko, co widzi druga strona.",
        ["Couldn't save it. The link may have been turned off."] = "Nie udało się tego zapisać. Link mógł zostać wyłączony.",
        ["You already have this."] = "Już to masz.",
        ["Saved. It's in your account now."] = "Zapisano. Jest teraz na Twoim koncie.",
        ["\"{0}\" is no longer available to you."] = "„{0}” nie jest już dla Ciebie dostępne.",
        ["(can't be opened on this device)"] = "(nie da się otworzyć na tym urządzeniu)",
        ["Unreadable - encrypted with an older key"] = "Nieczytelne — zaszyfrowane starszym kluczem",
        ["another list"] = "inną listą",
        ["another user"] = "inny użytkownik",
        ["This username is already taken."] = "Ta nazwa użytkownika jest już zajęta.",
        ["An account with this email address already exists."] = "Konto z tym adresem e-mail już istnieje.",
        ["Couldn't verify that Google account."] = "Nie udało się zweryfikować tego konta Google.",
        ["That didn't work."] = "Nie udało się.",
        ["Code sent to {0}. It expires in 15 minutes."] = "Kod wysłany na {0}. Wygasa za 15 minut.",

        // The label a markup expression picks between - a button that changes what it says.
        ["Create a link anyone can open"] = "Utwórz link, który każdy może otworzyć",
        ["Hide event list"] = "Ukryj listę wydarzeń",
        ["Hide task list"] = "Ukryj listę zadań",
        ["Hide map"] = "Ukryj mapę",
        ["New event"] = "Nowe wydarzenie",
        ["New note"] = "Nowa notatka",
        ["New task list"] = "Nowa lista zadań",
        ["Show names"] = "Pokaż nazwy",
        ["(shared)"] = "(udostępnione)",
        ["Demote"] = "Odbierz uprawnienia",
        ["Record where I am"] = "Zapisz, gdzie jestem",
        ["Update to where I am now"] = "Zaktualizuj na moje obecne położenie",
        ["Recorded location"] = "Zapisane położenie",
        ["Preparing…"] = "Przygotowywanie…",
        ["Orbit — Shared"] = "Orbit — Udostępnione",

        // The router's own two screens - what it shows while deciding, and where it lands
        // when there is nothing to show.
        ["Checking permissions…"] = "Sprawdzanie uprawnień…",
        ["Orbit — Not found"] = "Orbit — Nie znaleziono",
        ["This page doesn't exist."] = "Tej strony nie ma.",
        ["Email address"] = "Adres e-mail",
        ["Google extras"] = "Dodatki Google",
        ["Connecting Google lets you sign in with it as well as with your password."] = "Połączenie z Google pozwala logować się nim tak samo jak hasłem.",

        // Sentences with a value in the middle of them. The whole sentence is the key, because a
        // Polish one does not put its pieces in English's order.
        ["+{0} more"] = "+{0} więcej",
        ["Asked {0}. They'll see it in your chat."] = "Poproszono {0}. Zobaczy to w rozmowie z Tobą.",
        ["{0} hasn't set up encryption yet - they need to log in in their own browser first before you can message them."] = "{0} nie skonfigurował jeszcze szyfrowania — musi najpierw zalogować się we własnej przeglądarce, zanim będziesz mógł napisać.",
        ["{0} wants to chat with you."] = "{0} chce z Tobą porozmawiać.",
        ["Message is waiting for {0} to approve."] = "Wiadomość czeka na zgodę: {0}.",
        ["Forwarded from {0}"] = "Przekazane od: {0}",
        ["{0} lists"] = "listy: {0}",
        ["{0} members"] = "członkowie: {0}",
        ["{0} sharing"] = "udostępnienia: {0}",
        ["Created:"] = "Utworzono:",
        ["Last updated:"] = "Ostatnia zmiana:",
        ["Everything from the last {0}, newest first — cleared ones included. Older than that and they are deleted; change how long in"] = "Wszystko z ostatnich {0}, od najnowszych — łącznie z wyczyszczonymi. Starsze są usuwane; długość zmienisz w sekcji ",
        ["day"] = "dzień",
        ["Group list - {0} linked {1} shown below."] = "Lista grupowa — poniżej powiązane listy ({0}).",
        ["list"] = "lista",
        ["lists"] = "listy",
        ["follows {0}"] = "wynika z: {0}",
        ["due {0}"] = "termin: {0}",
        ["No lists are {0}."] = "Żadna lista nie ma stanu: {0}.",
        ["and {0} more…"] = "i jeszcze {0}…",
        ["Shared by {0} · {1}"] = "Udostępnił {0} · {1}",
        ["{0} is editing it right now"] = "{0} właśnie to edytuje",
        ["Shared by {0}"] = "Udostępnił {0}",
        ["Pick on map"] = "Wskaż na mapie",
        ["Coordinates: {0}, {1}"] = "Współrzędne: {0}, {1}",

        // The dashboard's short "when" labels.
        ["just now"] = "przed chwilą",
        ["{0}m ago"] = "{0} min temu",
        ["{0}h ago"] = "{0} godz. temu",
        ["Tomorrow"] = "Jutro",

        // The other half of a label that toggles - the state the first half does not cover.
        ["Copy the link"] = "Skopiuj link",
        ["Show event list"] = "Pokaż listę wydarzeń",
        ["Show task list"] = "Pokaż listę zadań",
        ["Hide details"] = "Ukryj szczegóły",
        ["Show details"] = "Pokaż szczegóły",
        ["View"] = "Podejrzyj",
        ["Edit event"] = "Edytuj wydarzenie",
        ["Edit note"] = "Edytuj notatkę",
        ["Edit task list"] = "Edytuj listę zadań",
        ["Hide names"] = "Ukryj nazwy",
        ["[couldn't decrypt]"] = "[nie udało się odszyfrować]",
        [" · Read"] = " · Przeczytane",
        [" · Sent"] = " · Wysłane",
        ["Make admin"] = "Nadaj uprawnienia",
        ["live"] = "na żywo",
        ["sent once"] = "wysłane raz",
        ["Hide this item's other settings"] = "Ukryj pozostałe ustawienia tej pozycji",
        ["Show this item's other settings"] = "Pokaż pozostałe ustawienia tej pozycji",
    };
}
