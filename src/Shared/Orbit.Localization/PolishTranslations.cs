namespace Orbit.Localization;

/// <summary>
/// Orbit's interface in Polish, keyed by the English it replaces. The key being the English text
/// itself is what lets a string with no translation fall back to correct English rather than to a hole,
/// which is what makes it safe to translate a screen at a time.
///
/// Shared by the web and the phone. They show different screens but say many of the same things, and a
/// second copy of this would drift the moment one of them was corrected.
///
/// Grouped by where the text appears rather than alphabetically, so a page can be checked against the
/// screen it belongs to. Anything not listed here stays in English, which is the fallback by design.
/// </summary>
public static class PolishTranslations
{
    public static readonly IReadOnlyDictionary<string, string> ByEnglish = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // ---- Navigation and the app shell ----
        ["Nothing here matches the filter. Change it above to see the rest."] = "Nic tutaj nie pasuje do filtra. Zmień go powyżej, aby zobaczyć resztę.",
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
        ["Pin to top"] = "Przypnij na górę",
        ["This was shared with you to read, not to change."] = "To zostało Ci udostępnione do czytania, nie do zmieniania.",
        ["Couldn't delete that note. Try again."] = "Nie udało się usunąć tej notatki. Spróbuj ponownie.",
        ["That note was already gone. The list has been brought up to date."] = "Tej notatki już nie było. Lista została odświeżona.",
        ["Orbit refused that change."] = "Orbit odrzucił tę zmianę.",
        ["Can't move this item right now - {0} is editing one of the lists."] = "Nie można teraz przenieść tej pozycji — {0} edytuje jedną z list.",
        ["Couldn't recalculate against the inventory."] = "Nie udało się przeliczyć względem inventory.",
        ["This list isn't measured against an inventory."] = "Ta lista nie jest porównywana z żadnym inventory.",
        ["Couldn't generate an inventory from this list."] = "Nie udało się wygenerować inventory z tej listy.",
        ["Inventory generated from this list."] = "Inventory wygenerowane z tej listy.",
        ["Recalculate against the inventory"] = "Przelicz względem inventory",
        ["Generate inventory"] = "Generuj inventory",
        ["More"] = "Więcej",
        ["Couldn't add those to the restock list."] = "Nie udało się dodać ich do listy uzupełnień.",
        ["{0} added to the restock list."] = "Dodano do listy uzupełnień: {0}.",
        ["Everything short was already on the restock list."] = "Wszystkie braki już były na liście uzupełnień.",
        ["Add what is missing to the restock list"] = "Dodaj braki do listy uzupełnień",
        ["Short"] = "Brakuje",
        ["On the shelf"] = "Na stanie",
        ["Needed"] = "Potrzeba",
        ["What"] = "Co",
        ["{0} of what this needs is short."] = "Brakuje pozycji: {0}.",
        ["Everything this list needs is on the shelf."] = "Wszystko, czego ta lista potrzebuje, jest na stanie.",
        ["That storage is already measured against another list."] =
            "Ten magazyn jest już mierzony względem innej listy.",
        ["Couldn't measure this list against that storage. Try again."] =
            "Nie udało się zmierzyć tej listy względem tego magazynu. Spróbuj ponownie.",
        ["Not measured against a warehouse"] = "Bez porównania z magazynem",
        ["Can this be done?"] = "Czy da się to wykonać?",
        ["Nothing due in this period."] = "Nic z terminem w tym okresie.",
        ["Nothing in this period."] = "Nic w tym okresie.",
        ["View saved"] = "Widok zapisany",
        ["Save view"] = "Zapisz widok",
        ["Show the lists"] = "Pokaż listy",
        ["Show single items"] = "Pokaż pojedyncze elementy",
        ["In list order"] = "W kolejności listy",
        ["Left to do first"] = "Najpierw do zrobienia",
        ["Short first"] = "Najpierw braki",
        ["This list"] = "Ta lista",

        // ---- The names Orbit writes for itself, which the server stores in English - see OrbitWrittenNames. ----
        ["You already have \"{0}\"."] = "Masz już „{0}”.",
        ["Expires in"] = "Traci ważność za",
        ["No date"] = "Bez daty",
        ["months"] = "miesięcy",
        ["years"] = "lat",
        ["\"{0}\" is reminded daily, so it needs a time to be reminded at."] = "„{0}” ma przypomnienie dzienne, więc potrzebuje godziny.",
        ["On the shelf in {0}. Saving this list saves the change there too."] = "Na półce w: {0}. Zapis tej listy zapisuje też tę zmianę.",
        ["What this entry names becomes a product when a storage is made from this list."] =
            "To, co ta pozycja nazywa, stanie się produktem, gdy z tej listy powstanie magazyn.",
        ["This entry isn't tied to a product yet, so there is nothing to edit here."] = "Ta pozycja nie jest jeszcze powiązana z produktem, więc nie ma tu czego edytować.",
        ["No other list yet"] = "Nie ma jeszcze innej listy",
        ["The list was saved, but the shelf couldn't be updated. Open the warehouse and check it."] = "Lista została zapisana, ale nie udało się zaktualizować magazynu. Otwórz magazyn i sprawdź.",
        ["Pick a point on the map, or type what to call this place"] = "Wskaż punkt na mapie albo wpisz, jak nazwać to miejsce",
        ["The name is yours to write - the pin below keeps its exact position either way."] = "Nazwę piszesz sam – pinezka i tak zachowuje swoje dokładne położenie.",
        ["Use the address from the map"] = "Użyj adresu z mapy",
        ["What this is about"] = "Czego to dotyczy",
        ["This errand's product needs a name and an amount."] = "Produkt tego sprawunku potrzebuje nazwy i ilości.",
        ["This needs a connection. It will work again once you're back online."] = "To wymaga połączenia. Zadziała ponownie, gdy wrócisz online.",
        ["Reconnect"] = "Połącz ponownie",
        ["offline"] = "offline",
        ["online"] = "online",
        ["Saved on this phone - the appointment reaches the calendar when you're back online."] = "Zapisano na tym telefonie — wydarzenie trafi do kalendarza po powrocie połączenia.",
        ["Somebody else can change this appointment, and Orbit can't be reached to check. It stays as it was until you're back online."] = "Ktoś inny może zmieniać to wydarzenie, a Orbit jest nieosiągalny, żeby to sprawdzić. Zostaje bez zmian do czasu powrotu połączenia.",
        ["Repeats"] = "Powtarza się",
        ["Does not repeat"] = "Nie powtarza się",
        ["Never"] = "Nigdy",
        ["Remind before"] = "Przypomnij wcześniej",
        ["No reminder"] = "Bez przypomnienia",
        ["Starts"] = "Zaczyna się",
        ["Ends"] = "Kończy się",
        ["This ends before it starts."] = "To kończy się przed rozpoczęciem.",
        ["This entry has an event in the calendar. Saving keeps the two in step."] = "Ta pozycja ma wydarzenie w kalendarzu. Zapis utrzymuje je w zgodzie.",
        ["Detach from the event"] = "Odłącz od wydarzenia",
        ["About this note"] = "O tej notatce",
        ["Write something in it."] = "Napisz w niej coś.",
        ["Related inventory"] = "Powiązany magazyn",
        ["Done: {0}"] = "Zrobione: {0}",
        ["About this list"] = "O tej liście",
        ["About this warehouse"] = "O tym magazynie",
        ["No contacts yet."] = "Brak kontaktów.",
        ["Banner only"] = "Tylko baner",
        ["Push and email"] = "Push i e-mail",
        ["Check every round"] = "Sprawdzaj co obchód",
        ["Always on the restock list, however much there is"] = "Zawsze na liście uzupełnień, niezależnie od stanu",
        ["Archive"] = "Archiwum",
        ["Put back"] = "Przywróć",
        ["Nothing put away."] = "Nic nie odłożono.",
        ["Could not put that away. Check your connection and try again."] = "Nie udało się odłożyć. Sprawdź połączenie i spróbuj ponownie.",
        ["Could not put that back. Check your connection and try again."] = "Nie udało się przywrócić. Sprawdź połączenie i spróbuj ponownie.",
        ["Notification channel"] = "Sposób powiadamiania",
        ["Also when it starts"] = "Także gdy się zaczyna",
        ["Repeat how many times"] = "Ile razy powtórzyć",
        ["no limit"] = "bez limitu",
        ["Yearly"] = "Co roku",
        ["Permission"] = "Uprawnienia",
        ["6 hours before"] = "6 godzin wcześniej",
        ["The name is yours to write - the pin keeps its exact position either way."] = "Nazwę piszesz sam - pinezka i tak trzyma swoje dokładne położenie.",
        ["Point at this place on the map so the calendar knows where it is - a name on its own stays as words."] = "Wskaż to miejsce na mapie, żeby kalendarz wiedział, gdzie jest - sama nazwa zostaje słowami.",
        ["Stands for these lists"] = "Odpowiada za listy",
        ["Add another…"] = "Dodaj kolejną…",
        ["Completion follows the linked lists, so it can't be ticked by hand."] = "Ukończenie idzie za powiązanymi listami, więc nie da się go zaznaczyć ręcznie.",
        ["Delete chat history"] = "Usuń historię czatu",
        ["Leave and delete chat history"] = "Opuść grupę i usuń historię",
        ["Could not delete that chat history. Check your connection and try again."] = "Nie udało się usunąć historii. Sprawdź połączenie i spróbuj ponownie.",
        ["Could not leave that group. Check your connection and try again."] = "Nie udało się opuścić grupy. Sprawdź połączenie i spróbuj ponownie.",
        ["Private note"] = "Notatka prywatna",
        ["Something happened here"] = "Coś się tu wydarzyło",
        ["This place goes to the event in the calendar, pin and all."] = "To miejsce trafia do wydarzenia w kalendarzu, razem z pinezką.",
        ["Point at this place on the map so the calendar knows where it is - a name on its own stays on the entry."] = "Wskaż to miejsce na mapie, aby kalendarz wiedział, gdzie ono jest - sama nazwa zostaje przy pozycji.",
        ["\"{0}\" already has an event in the calendar, so its type can't be changed. Detach it from the event first, then decide what to do with the event itself."] = "„{0}” ma już wydarzenie w kalendarzu, więc nie można zmienić jego typu. Najpierw odłącz je od wydarzenia, potem zdecyduj, co zrobić z samym wydarzeniem.",
        ["\"{0}\" is a calendar entry, so it needs a day to happen on."] = "„{0}” to pozycja kalendarza, więc potrzebuje dnia, w którym się odbywa.",
        ["\"{0}\" ends before it starts."] = "„{0}” kończy się przed rozpoczęciem.",
        ["\"{0}\" couldn't be put in the calendar, so nothing was saved."] = "Nie udało się umieścić „{0}” w kalendarzu, więc nic nie zostało zapisane.",
        // "View" and "Normal" already mean other things here - "Podejrzyj" on a button that opens
        // something, "Zwykły" as a priority - so these get English of their own rather than a second
        // entry under the same key. See PolishTranslationsTests.
        ["Card view"] = "Widok",
        ["Minimal"] = "Minimalistyczny",
        ["Normal view"] = "Normalny",
        ["Full"] = "Pełny",
        ["Restock list"] = "Lista uzupełnień",
        ["Only what a dated task is waiting on"] = "Tylko to, na co czeka zadanie z terminem",
        ["The list asks for products some task with a due date needs. What is running low but nothing is waiting on is left off."] = "Lista prosi o produkty potrzebne zadaniu z terminem. To, czego brakuje, ale nic na to nie czeka, zostaje pominięte.",
        ["The list asks for everything on this shelf that has dropped below its own minimum."] = "Lista prosi o wszystko na tej półce, co spadło poniżej własnego minimum.",
        ["Comes round at"] = "Przypomina o",
        ["When the standing \"Update stock levels\" reminder arrives."] = "Kiedy przychodzi stałe przypomnienie „Zaktualizuj stany magazynowe”.",
        ["Save settings"] = "Zapisz ustawienia",
        ["Refresh"] = "Odśwież",
        ["The restock list already asks for exactly what it should."] = "Lista uzupełnień prosi dokładnie o to, o co powinna.",
        ["Restock list updated: {0} added, {1} removed."] = "Lista uzupełnień zaktualizowana: dodano {0}, usunięto {1}.",
        ["That didn't work. Try again."] = "Nie udało się. Spróbuj ponownie.",
        ["api {0}"] = "api {0}",
        ["Show the whole commit"] = "Pokaż pełny hash commita",
        ["Version {0}"] = "Wersja {0}",
        ["All Rights Reserved"] = "Wszelkie prawa zastrzeżone",
        ["About"] = "O aplikacji",
        ["in {0}"] = "w {0}",
        ["also on {0}"] = "także na {0}",
        ["Restock supplies"] = "Uzupełnienie zapasów",
        ["Restock:"] = "Uzupełnij:",
        ["Update stock levels"] = "Zaktualizuj stany magazynowe",

        ["{0} crossed off, because the warehouse covers them."] = "Odhaczono {0}, bo magazyn je pokrywa.",
        ["{0} added from the warehouse."] = "Dodano z magazynu: {0}.",
        ["{0} crossed off, because the warehouse covers them, and {1} added from the warehouse."] =
            "Odhaczono {0}, bo magazyn je pokrywa, oraz dodano z magazynu: {1}.",
        ["Nothing new is covered by the warehouse."] = "Magazyn nie pokrywa niczego nowego.",
        ["Finish this list and set every item in the warehouse to its minimum?"] =
            "Czy chcesz zakończyć zadanie i ustawić wszystkim elementom minimalną wartość na magazynie?",
        // The two answers to the question above. The web has the browser's own OK and Cancel; a phone
        // alert names its buttons, and "OK" would not say which of the two things it does.
        ["Finish the whole list"] = "Zakończ całą listę",
        ["Just this one"] = "Tylko to",
        ["{0} brought up to their minimum."] = "Uzupełniono do minimum: {0}.",
        ["Couldn't finish the restocking. Try again."] = "Nie udało się zakończyć uzupełniania. Spróbuj ponownie.",
        ["Couldn't recalculate against the warehouse. Try again."] =
            "Nie udało się przeliczyć względem magazynu. Spróbuj ponownie.",
        ["Hide"] = "Ukryj",
        ["Show on the dashboard"] = "Pokaż na pulpicie",
        ["Show"] = "Pokaż",
        ["Search conversations"] = "Szukaj rozmów",
        ["Orbit can't reach that account, so this conversation can't be opened right now."] =
            "Orbit nie może połączyć się z tym kontem, więc tej rozmowy nie da się teraz otworzyć.",
        ["Sorts this note against the others, and is what the dashboard's filter reads."] =
            "Sortuje tę notatkę względem innych i jest tym, co czyta filtr na pulpicie.",
        ["Sorts this event against the others, and is what the dashboard's filter reads."] =
            "Sortuje to wydarzenie względem innych i jest tym, co czyta filtr na pulpicie.",
        ["Group chat"] = "Czat grupowy",
        ["Everything here is hidden. The menu at the top right brings it back."] =
            "Wszystko jest ukryte. Menu w prawym górnym rogu przywraca elementy.",
        ["Drag to reorder"] = "Przeciągnij, aby zmienić kolejność",
        ["Minimise"] = "Zminimalizuj",
        ["Open the calendar"] = "Otwórz kalendarz",
        ["Expand"] = "Rozwiń",
        // Said out loud in place of a glyph, for a reader who cannot see which way it points.
        ["Collapse"] = "Zwiń",
        ["Earlier"] = "Wcześniej",
        ["Later"] = "Później",
        ["Cancel reply"] = "Anuluj odpowiedź",
        ["Nothing left to do."] = "Nic nie zostało do zrobienia.",
        ["Pick a date"] = "Wybierz datę",
        ["Previous month"] = "Poprzedni miesiąc",
        ["Next month"] = "Następny miesiąc",
        ["Opens Google's own form with this event filled in. Nothing is saved there until you save it."] = "Otwiera formularz Google z wypełnionym wydarzeniem. Nic się tam nie zapisze, dopóki sam nie zapiszesz.",
        ["Google Calendar"] = "Kalendarz Google",
        ["Recording where you are. Sharing it, or seeing somebody else's, also needs contacts."] = "Zapisywanie swojej pozycji. Udostępnienie jej lub podgląd cudzej wymaga też kontaktów.",
        ["Conversations, with one person or with several."] = "Rozmowy — z jedną osobą lub z kilkoma.",
        ["Finding other people, and being found by them. Everything below needs this first."] = "Znajdowanie innych osób i bycie znajdowanym przez nie. Wszystko poniżej tego wymaga.",
        ["shared a position"] = "udostępnił pozycję",
        ["You"] = "Ty",
        ["Needs {0}"] = "Wymaga: {0}",
        ["{0} has to be unlocked first."] = "Najpierw trzeba odblokować: {0}.",
        ["Open Options"] = "Otwórz opcje",
        ["Enter the code for it under Options, Permissions."] = "Wpisz kod do tej części w Opcjach, w zakładce Uprawnienia.",
        ["Not unlocked yet"] = "Jeszcze nieodblokowane",
        ["Couldn't reach the server. Try again."] = "Nie udało się połączyć z serwerem. Spróbuj ponownie.",
        ["{0} is now unlocked."] = "{0} — odblokowane.",
        ["That code doesn't unlock anything."] = "Ten kod niczego nie odblokowuje.",
        ["e.g. 7Q31KS0KB4Y0"] = "np. 7Q31KS0KB4Y0",
        ["Code"] = "Kod",
        ["One code per part of Orbit. Whoever deployed this Orbit has them - they are generated when it is built, and change every time it is."] = "Jeden kod na każdą część Orbita. Ma je ten, kto wdrożył tego Orbita — powstają przy budowaniu i zmieniają się z każdą kolejną.",
        ["Unlock code"] = "Kod odblokowujący",
        ["Handing a note, task list, event or storage to somebody else."] = "Przekazanie komuś notatki, listy zadań, wydarzenia lub magazynu.",
        ["Conversations with more than one other person."] = "Rozmowy z więcej niż jedną osobą.",
        ["Conversations with one other person."] = "Rozmowy z jedną osobą.",
        ["Recording where you are, sharing it, and seeing where others are."] = "Zapisywanie swojej pozycji, udostępnianie jej i podgląd cudzych.",
        ["Chat"] = "Czat",
        ["Locked"] = "Zablokowane",
        ["Unlocked"] = "Odblokowane",
        ["State"] = "Stan",
        ["Part of Orbit"] = "Część Orbita",
        ["Parts of Orbit this account can use. Each is unlocked separately, with its own code."] = "Części Orbita, z których to konto może korzystać. Każdą odblokowuje się osobno, własnym kodem.",
        ["In Orbit"] = "W Orbicie",
        ["Permissions"] = "Uprawnienia",
        ["Available"] = "Dostępny",
        ["Away"] = "Zaraz wracam",
        ["Do not disturb"] = "Nie przeszkadzać",
        ["Offline"] = "Niedostępny",
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
        ["Email or login"] = "E-mail lub login",
        ["Password"] = "Hasło",
        ["Repeat password"] = "Powtórz hasło",
        ["Display name"] = "Nazwa wyświetlana",
        ["Email"] = "E-mail",
        // Same word in Polish - written down rather than left out, so the coverage test can tell a
        // deliberate match from an entry somebody forgot.
        ["Push"] = "Push",
        ["Login"] = "Login",
        ["Don't have an account? "] = "Nie masz konta? ",
        ["Already have an account? "] = "Masz już konto? ",
        ["Invalid email, login, or password."] = "Nieprawidłowy e-mail, login lub hasło.",
        ["No account uses that email address or login."] = "Żadne konto nie ma takiego adresu e-mail ani loginu.",
        ["That password is wrong."] = "Nieprawidłowe hasło.",
        ["This account signs in with Google and has no password yet. Use the Google button below."] =
            "To konto loguje się przez Google i nie ma jeszcze hasła. Skorzystaj z przycisku Google poniżej.",

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

        // What a note copied for editing offline is called, and the two windows that exist because of
        // it - the review that asks which version to keep, and the history of the ones kept.
        ["Make a copy you can write in while you are offline?"] =
            "Zrobić kopię, w której możesz pisać bez połączenia?",
        ["Make a copy"] = "Zrób kopię",
        ["Not now"] = "Nie teraz",
        ["copy"] = "kopia",
        ["Copies to review"] = "Kopie do przejrzenia",
        ["What you wrote while you were offline, beside what it came from."] =
            "To, co napisałeś bez połączenia, obok tego, z czego to powstało.",
        ["Nothing to review."] = "Nie ma nic do przejrzenia.",
        ["This note was also changed elsewhere."] = "Ta notatka zmieniła się też gdzie indziej.",
        ["The note this came from is gone."] = "Notatki, z której to powstało, już nie ma.",
        // What each kind is called, for the two copy windows that hold all four at once. "Note",
        // "Task list" and "Warehouse" are already translated above, under the words the whole app shares.
        ["Appointment"] = "Spotkanie",
        ["Your copy"] = "Twoja kopia",
        ["Changed elsewhere"] = "Zmienione gdzie indziej",
        ["Keep mine"] = "Zostaw moją",
        ["Keep theirs"] = "Zostaw tamtą",
        ["Keep both"] = "Zostaw obie",
        ["History"] = "Historia",
        ["Copies of this, and what each came from."] = "Kopie tego elementu i to, z czego każda powstała.",
        ["Nothing has been copied from this."] = "Nic nie zostało z tego skopiowane.",
        ["Still waiting to be reviewed"] = "Wciąż czeka na przejrzenie",
        ["The original is gone - its owner may have deleted it. Do you want to keep your copy?"] =
            "Nie znaleziono oryginału - możliwe, że właściciel go usunął. Czy chcesz zachować swoją kopię?",
        ["Keep my copy"] = "Zachowaj moją kopię",
        ["Open the original"] = "Otwórz oryginał",
        ["{0} · copy of “{1}”, made on {2}."] = "{0} · kopia „{1}”, zrobiona {2}.",
        ["{0} · copied on {1}. What it came from is gone."] =
            "{0} · skopiowane {1}. Tego, z czego to powstało, już nie ma.",
        ["Orbit can't be reached to check who else is editing. Try this again once you're back online."] =
            "Nie można połączyć się z Orbitem, żeby sprawdzić, kto jeszcze to edytuje. Spróbuj ponownie, gdy wrócisz online.",
        ["That is no longer here."] = "Tego już tu nie ma.",
        // What a share's second picker is called, so a screen reader does not read both as "Share with".
        ["What they may do"] = "Co mogą robić",
        ["How the daily reminder arrives"] = "Jak przychodzi codzienne przypomnienie",
        // What the banner offers when a notification arrives with the app in front - see ForegroundNotices.
        ["Dismiss"] = "Odrzuć",
        // ---- What a list or a warehouse is for, under its name - see TaskListDetailViewModel. ----
        ["What is it for?"] = "Do czego to jest?",
        // ---- Putting a conversation away, emptying it, and leaving a group - see ContactsViewModel. ----
        ["Show what is put away"] = "Pokaż odłożone",
        ["Orbit has no such conversation any more."] = "Orbit nie ma już takiej rozmowy.",
        ["Orbit has no such group any more."] = "Orbit nie ma już takiej grupy.",
        ["Everything in this conversation goes, on your side only. This cannot be undone."] =
            "Cała ta rozmowa zniknie, tylko po Twojej stronie. Tego nie da się cofnąć.",
        ["You stop receiving what is posted, and the group sees you go."] =
            "Przestaniesz dostawać to, co tam trafia, a grupa zobaczy, że wychodzisz.",
        // ---- Shared without permission to edit - see SharedItemAccess. ----
        ["Shared with you to read. Ask whoever shared it if you need to change it."] =
            "Udostępnione Ci do odczytu. Poproś osobę, która udostępniła, jeśli musisz to zmienić.",
        // ---- A public link opened in the app rather than a browser - see SharedLinkViewModel. ----
        ["Shared with a link"] = "Udostępnione linkiem",
        ["This link couldn't be opened. Try again."] = "Nie udało się otworzyć tego linku. Spróbuj ponownie.",
        ["A link can only be opened online. Try again when you are back."] =
            "Link da się otworzyć tylko online. Spróbuj ponownie, gdy wrócisz do sieci.",
        ["This couldn't be saved to your account. Try again."] =
            "Nie udało się zapisać tego na Twoim koncie. Spróbuj ponownie.",
        ["Saved. It will appear once Orbit next syncs."] =
            "Zapisano. Pojawi się przy następnej synchronizacji.",
        ["You get your own read-only copy. Nothing you do changes theirs."] =
            "Dostajesz własną kopię tylko do odczytu. Nic, co zrobisz, nie zmieni tamtej.",
        // ---- Getting back in without the password - see PasswordResetViewModel. ----
        ["Forgotten your password?"] = "Nie pamiętasz hasła?",
        ["Forgotten password"] = "Nie pamiętam hasła",
        ["Enter the account, and Orbit emails a code to the address it was registered with."] =
            "Podaj konto, a Orbit wyśle kod na adres, na który zostało założone.",
        ["Getting back into an account needs a connection to Orbit."] =
            "Odzyskanie konta wymaga połączenia z Orbitem.",
        ["Send a code"] = "Wyślij kod",
        ["A new password starts your chat over: messages sealed with the old one stay unreadable, because Orbit never had their key."] =
            "Nowe hasło zaczyna czat od nowa: wiadomości zapieczętowane starym pozostaną nie do odczytania, bo Orbit nigdy nie miał do nich klucza.",
        ["Set the new password"] = "Ustaw nowe hasło",
        ["Back to signing in"] = "Wróć do logowania",
        ["If that account exists, a code is on its way to the address it was registered with."] =
            "Jeśli takie konto istnieje, kod jest już w drodze na adres, na który je założono.",
        ["Password changed. Sign in with the new one."] = "Hasło zmienione. Zaloguj się nowym.",
        // ---- The notification settings the phone obeys and could not set - see NotificationSettingsViewModel. ----
        ["Banner while Orbit is open"] = "Baner, gdy Orbit jest otwarty",
        ["Shows what arrived at the top of the screen instead of only in the tray."] =
            "Pokazuje to, co przyszło, u góry ekranu, a nie tylko na pasku powiadomień.",
        ["How long it stays"] = "Jak długo widoczny",
        ["Quiet gap before the next one"] = "Przerwa przed kolejnym",
        ["Keep notifications for"] = "Przechowuj powiadomienia przez",
        ["Clearing the panel only tidies them away. This is what deletes them for good."] =
            "Wyczyszczenie panelu tylko je porządkuje. To ustawienie usuwa je na dobre.",
        // ---- The home screen widget - see TodayAtAGlance. ----
        ["Nothing left today"] = "Nic już na dziś",
        ["Open Orbit to see your day"] = "Otwórz Orbita, żeby zobaczyć swój dzień",
        ["{0} more"] = "jeszcze {0}",
        ["Saved, but that place could not be found - use your location to keep a point for it."] =
            "Zapisano, ale nie udało się znaleźć tego miejsca - użyj swojej lokalizacji, żeby zapisać punkt.",
        ["Saved, but that place could not be found - open the map and point at it to keep it."] =
            "Zapisano, ale nie udało się znaleźć tego miejsca - otwórz mapę i wskaż je, żeby je zachować.",

        // What rebuilding a warehouse's restock list moved, and what it needs - see the phone's
        // RestockListSettingsPanel, which shows the settings Orbit.Web has had all along.
        ["Added {0}, removed {1}."] = "Dodano {0}, usunięto {1}.",
        ["The restock list needs a connection."] = "Lista uzupełnień wymaga połączenia.",

        // What the phone tells itself, in the notification feed - see LocalNotification.IsRaisedHere.
        // One whole sentence per kind, because Polish declines what was refused or copied.
        ["A change couldn't be saved"] = "Nie udało się zapisać zmiany",
        ["Orbit couldn't save a change to a note, so it is no longer waiting to be sent."] =
            "Orbitowi nie udało się zapisać zmiany w notatce, więc nie czeka już na wysłanie.",
        ["Orbit couldn't save a change to a task list, so it is no longer waiting to be sent."] =
            "Orbitowi nie udało się zapisać zmiany na liście zadań, więc nie czeka już na wysłanie.",
        ["Orbit couldn't save a change to an appointment, so it is no longer waiting to be sent."] =
            "Orbitowi nie udało się zapisać zmiany w spotkaniu, więc nie czeka już na wysłanie.",
        ["Orbit couldn't save a change to a warehouse, so it is no longer waiting to be sent."] =
            "Orbitowi nie udało się zapisać zmiany w magazynie, więc nie czeka już na wysłanie.",
        ["Orbit couldn't save a change, so it is no longer waiting to be sent."] =
            "Orbitowi nie udało się zapisać zmiany, więc nie czeka już na wysłanie.",
        ["A copy is waiting to be reviewed"] = "Kopia czeka na przejrzenie",
        ["You wrote in a copy of the note “{0}” while you were offline."] =
            "Bez połączenia pisałeś w kopii notatki „{0}”.",
        ["You wrote in a copy of the task list “{0}” while you were offline."] =
            "Bez połączenia pisałeś w kopii listy zadań „{0}”.",
        ["You wrote in a copy of the appointment “{0}” while you were offline."] =
            "Bez połączenia pisałeś w kopii spotkania „{0}”.",
        ["You wrote in a copy of the warehouse “{0}” while you were offline."] =
            "Bez połączenia pisałeś w kopii magazynu „{0}”.",

        // ---- Tasks ----
        ["Add task list"] = "Dodaj listę zadań",
        ["Every list you own or someone shared with you."] = "Każda lista, którą masz albo którą Ci udostępniono.",
        ["No task lists yet."] = "Nie ma jeszcze żadnych list zadań.",
        ["No items on this list."] = "Ta lista jest pusta.",
        ["Open checklist"] = "Otwórz listę",
        ["Sort"] = "Sortuj",
        // The calendar's list beside the grid - see CalendarListSortOrder.
        ["By when"] = "Po dacie",
        ["By type"] = "Po typie",
        ["Alphabetical"] = "Alfabetycznie",
        ["Newest first"] = "Od najnowszych",
        ["Oldest first"] = "Od najstarszych",
        ["A to Z"] = "Od A do Z",
        ["Z to A"] = "Od Z do A",
        ["Most important first"] = "Od najważniejszych",
        ["Least important first"] = "Od najmniej ważnych",
        ["The way I arranged them"] = "Po mojemu",
        ["All"] = "Wszystkie",
        ["Not started"] = "Nierozpoczęte",
        ["In progress"] = "W trakcie",
        ["Overdue"] = "Po terminie",
        ["Items"] = "Pozycje",
        ["Group View"] = "Widok grupowy",
        ["Type"] = "Typ",
        ["Calendar event"] = "Wydarzenie w kalendarzu",
        ["Back to Calendar"] = "Wróć do kalendarza",
        ["Couldn't change your availability. Check your connection and try again."] = "Nie udało się zmienić dostępności. Sprawdź połączenie i spróbuj ponownie.",
        ["Show map"] = "Pokaż mapę",
        ["Pick a place"] = "Wskaż miejsce",
        ["Search for an address"] = "Wyszukaj adres",
        ["Searching…"] = "Szukam…",
        ["Nothing found for that. Try fewer words, or point at it on the map."] = "Nic nie znaleziono. Spróbuj krócej albo wskaż miejsce na mapie.",
        ["Click the map to drop a pin."] = "Kliknij mapę, aby postawić pinezkę.",
        // The phone's own wording for the same map: nobody clicks a phone.
        ["Tap the map to drop a pin."] = "Dotknij mapy, aby postawić pinezkę.",
        ["Use this place?"] = "Użyć tego miejsca?",
        ["Yes, use it"] = "Tak, użyj",
        ["Looking that place up…"] = "Szukam tego miejsca…",
        ["That place has no address, so only the pin says where it is."] = "To miejsce nie ma adresu - o tym, gdzie jest, mówi tylko pinezka.",
        ["Unit"] = "Jednostka",
        ["What this is counted in"] = "W czym to jest liczone",
        ["Any type"] = "Dowolny typ",
        ["Any category"] = "Dowolna kategoria",
        ["Search items by name"] = "Szukaj pozycji po nazwie",
        ["Find an item in any warehouse"] = "Znajdź pozycję w dowolnym magazynie",
        ["Looking through your warehouses…"] = "Przeszukuję magazyny…",
        ["Nothing on any shelf matches that."] = "Nic na żadnej półce nie pasuje.",
        ["Found in {0} of {1} warehouses."] = "Znaleziono w {0} z {1} magazynów.",
        ["Found in {0} of {1} warehouses. {2} could not be opened, so nothing in them was searched."] =
            "Znaleziono w {0} z {1} magazynów. {2} nie udało się otworzyć, więc nic w nich nie zostało przeszukane.",
        ["Move up"] = "Przenieś w górę",
        ["Move down"] = "Przenieś w dół",
        ["Show everything"] = "Pokaż wszystko",
        ["Nothing here matches that. The rest of the warehouse is still there - clear the filter to see it."] = "Nic tu nie pasuje. Reszta magazynu nadal tam jest - wyczyść filtr, żeby ją zobaczyć.",
        ["Showing {0} of {1} items. Saving keeps all of them."] = "Widocznych {0} z {1} pozycji. Zapis zachowuje wszystkie.",
        ["Piece"] = "Sztuka",
        ["Kilogram"] = "Kilogram",
        ["Milligram"] = "Miligram",
        ["Litre"] = "Litr",
        ["Millilitre"] = "Mililitr",
        ["Pack"] = "Paczka",
        ["pcs"] = "szt.",
        ["kg"] = "kg",
        ["mg"] = "mg",
        ["l"] = "l",
        ["ml"] = "ml",
        ["pack"] = "opak.",
        ["Members"] = "Członkowie",
        ["Member"] = "Członek",
        ["Back to chat"] = "Wróć do czatu",
        ["That group is no longer one you're in."] = "Nie jesteś już w tej grupie.",
        ["Started"] = "Założona",
        ["Your role"] = "Twoja rola",
        ["Contact"] = "Kontakt",
        ["Who this is, and how to reach them."] = "Kto to jest i jak się z nim skontaktować.",
        ["Status"] = "Status",
        ["Last message"] = "Ostatnia wiadomość",
        ["They asked to chat with you. Open the conversation to allow it."] = "Ta osoba prosi o rozmowę. Otwórz konwersację, aby na nią pozwolić.",
        ["Waiting for them to allow this conversation."] = "Czekamy, aż ta osoba zgodzi się na rozmowę.",
        ["No conversation with them yet."] = "Nie ma jeszcze rozmowy z tą osobą.",
        ["There is nothing to show for this account. Either it does not exist, or the person has made themselves unfindable - Orbit answers both the same way, on purpose."] =
            "Nie ma czego pokazać dla tego konta. Albo nie istnieje, albo ta osoba ukryła się przed wyszukiwaniem - Orbit celowo odpowiada tak samo w obu przypadkach.",
        ["This person has made themselves unfindable in Orbit, or the account is gone. Orbit answers both the same way, so there is no telling which from here."] =
            "Ta osoba ukryła się przed wyszukiwaniem w Orbicie albo jej konto zniknęło. Orbit odpowiada tak samo w obu przypadkach, więc stąd nie da się tego rozstrzygnąć.",
        ["Your conversation with them is not affected - everything in it is still there, and still readable."] =
            "Wasza rozmowa jest nienaruszona - wszystko w niej nadal jest i nadal da się to przeczytać.",
        ["Couldn't load this contact. Check your connection and try again."] = "Nie udało się wczytać tego kontaktu. Sprawdź połączenie i spróbuj ponownie.",
        ["Conversation options"] = "Opcje rozmowy",
        ["Pick a conversation from the list, or start a new group."] = "Wybierz rozmowę z listy albo załóż nową grupę.",
        ["Show Tasks"] = "Pokaż zadania",
        ["When"] = "Kiedy",
        ["Where"] = "Gdzie",
        ["Details"] = "Szczegóły",
        ["No date set"] = "Brak terminu",
        ["No place set"] = "Brak miejsca",
        ["Already done."] = "Już zrobione.",
        ["That place could not be found on the map."] = "Nie udało się znaleźć tego miejsca na mapie.",
        ["That task item no longer exists."] = "Tej pozycji już nie ma.",
        ["Task"] = "Zadanie",
        ["Happens at {0}, which the event decides."] = "Odbywa się: {0} — decyduje o tym wydarzenie.",
        ["somewhere the event does not say"] = "w miejscu, którego wydarzenie nie podaje",
        ["Where this happens"] = "Gdzie się to odbywa",

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
        ["Directions"] = "Wyznacz trasę",
        ["Open in Google Maps"] = "Otwórz w Mapach Google",
        ["No list"] = "Żadna lista",
        ["The list gets an entry pointing at this event, not a copy of it."] =
            "Lista dostaje pozycję wskazującą to wydarzenie, a nie jego kopię.",
        ["The event is saved, but it couldn't be put on that list."] =
            "Wydarzenie zapisane, ale nie udało się dodać go do tej listy.",
        ["The event is saved, but {0} is editing that list - put it on the list again in a moment."] =
            "Wydarzenie zapisane, ale {0} edytuje tę listę — dodaj je do niej za chwilę.",
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
        ["Categories"] = "Kategorie",
        ["Separate them with commas"] = "Oddziel je przecinkami",
        ["Categories, separated by commas"] = "Kategorie, oddzielone przecinkami",
        ["Find an entry on any list"] = "Znajdź element na dowolnej liście",
        ["Entries in every chosen category"] = "Elementy w każdej wybranej kategorii",
        ["Nothing on any list matches that."] = "Nic na żadnej liście tego nie pasuje.",
        ["Expires"] = "Termin ważności",
        ["Private - encrypted, and only you can read it"] = "Prywatne — zaszyfrowane, czyta to tylko Ty",
        ["There is less of this than the minimum you set"] = "Zostało tego mniej niż ustawione minimum",
        ["How much of it there is"] = "Ile tego jest",
        ["Restock once the amount drops below this"] = "Uzupełnij, gdy ilość spadnie poniżej tego poziomu",

        // ---- Chat ----
        ["Chats"] = "Rozmowy",
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
        ["No chats. Search for a user by email address or login to start a conversation."] =
            "Brak rozmów. Wyszukaj kogoś po adresie e-mail lub loginie, żeby zacząć.",
        ["Continue with Google"] = "Kontynuuj z Google",
        ["Google couldn't sign you in to Orbit."] = "Google nie zalogowało Cię do Orbita.",
        ["or"] = "lub",
        ["Search for someone and start a conversation."] = "Znajdź kogoś i zacznij rozmowę.",
        ["Email address or login"] = "Adres e-mail lub login",

        // ---- Map ----
        ["Where you are, who you are sharing it with, and who is sharing theirs."] =
            "Gdzie jesteś, komu to udostępniasz i kto udostępnia swoje położenie Tobie.",
        ["Start recording"] = "Zacznij zapisywać",
        ["Stop recording"] = "Przestań zapisywać",
        ["Share where you are"] = "Udostępnij swoje położenie",
        ["You are sharing with"] = "Udostępniasz",
        ["Sharing with you"] = "Udostępniają Tobie",
        ["Nobody yet."] = "Na razie nikt.",
        ["Send once"] = "Wyślij raz",
        ["Keep sharing"] = "Udostępniaj na bieżąco",
        // Still said by the phone's own map screen - see Orbit.Maui's MapPage.xaml.
        ["Stop"] = "Zatrzymaj",
        ["Stop receiving"] = "Przestań odbierać",
        ["Search for a place"] = "Szukaj miejsca",
        ["Nothing found for that. Try fewer words."] = "Nic nie znaleziono. Spróbuj krótszej frazy.",
        ["What happens here?"] = "Co się tu dzieje?",
        ["An event in the calendar"] = "Wydarzenie w kalendarzu",
        ["A task list starting here"] = "Lista zadań zaczynająca się tutaj",
        ["Orbit isn't allowed to use your location. Turn it on in Options first."] =
            "Orbit nie ma zgody na korzystanie z Twojego położenia. Włącz ją najpierw w Opcjach.",

        // ---- Options ----
        ["Appearance"] = "Wygląd",
        ["Accent colour"] = "Kolor wiodący",
        ["The colour Orbit highlights things in. Kept on this device, like the theme."] =
            "Kolor, którym Orbit wyróżnia elementy. Zapamiętywany na tym urządzeniu, tak jak motyw.",
        ["No colour"] = "Bez koloru",
        ["Brown"] = "Brązowy",
        ["Violet"] = "Fioletowy",
        // For a colour an event carries that the palette does not offer - set in a browser, or left
        // from an older palette.
        ["Another colour"] = "Inny kolor",
        ["Purple"] = "Fioletowy",
        ["Blue"] = "Niebieski",
        ["Teal"] = "Turkusowy",
        ["Green"] = "Zielony",
        ["Amber"] = "Bursztynowy",
        ["Orange"] = "Pomarańczowy",
        ["Red"] = "Czerwony",
        ["Pink"] = "Różowy",
        ["System"] = "Systemowy",
        ["Light"] = "Jasny",
        ["Dark"] = "Ciemny",
        ["Language"] = "Język",
        ["The language Orbit's own interface is written in. Kept on this device."] =
            "Język, w którym napisany jest interfejs Orbita. Zapamiętywany na tym urządzeniu.",
        ["Location"] = "Położenie",
        ["Use my location"] = "Korzystaj z mojego położenia",
        ["Debugger"] = "Debugger",
        ["Debug logs"] = "Logi diagnostyczne",
        ["Logged so far"] = "Zapisane do tej pory",
        ["Happening now"] = "Dzieje się teraz",
        ["What Orbit reports about itself - the Debugger settings, the captured log, and the detail behind an error."] =
            "To, co Orbit mówi o sobie samym — ustawienia debuggera, zapisany log i szczegóły błędu.",
        ["Off"] = "Wyłączone",
        ["Kept on this device."] = "Zapamiętane na tym urządzeniu.",
        ["Mode"] = "Tryb",
        ["Release"] = "Release",
        ["Debug"] = "Debug",
        ["Frontend log level"] = "Poziom logowania frontendu",
        ["Your data"] = "Twoje dane",
        ["Export everything"] = "Wyeksportuj wszystko",
        ["Task lists"] = "Listy zadań",
        ["Calendar events"] = "Wydarzenia w kalendarzu",
        ["Storages"] = "Magazyny",
        ["Export"] = "Eksport",
        ["Import"] = "Import",
        ["Danger zone"] = "Strefa niebezpieczna",
        ["Allow notifications"] = "Zezwalaj na powiadomienia",
        ["Allow push"] = "Zezwalaj na push",
        ["Allow email"] = "Zezwalaj na e-mail",
        ["Tell me when something is shared with me"] = "Powiadom mnie, gdy ktoś coś mi udostępni",
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
        ["Orbit — Licence"] = "Orbit — Licencja",
        ["Licence"] = "Licencja",
        ["The licence couldn't be read from this Orbit."] = "Nie udało się odczytać licencji z tego Orbita.",
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

        // What a notification says. The server writes these as English sentences with {0}-style holes
        // rather than finishing them, because it never learns which language the reader has chosen -
        // see Orbit.Core's PushNotificationPayload. Both clients look them up here.
        ["New message"] = "Nowa wiadomość",
        ["New message from {0}"] = "Nowa wiadomość od: {0}",
        ["Event created"] = "Wydarzenie zapisane",
        ["The event \"{0}\" has been saved to your calendar."] = "Wydarzenie „{0}” zostało zapisane w Twoim kalendarzu.",
        ["Upcoming event"] = "Zbliżające się wydarzenie",
        ["The event \"{0}\" is starting now."] = "Wydarzenie „{0}” właśnie się zaczyna.",
        ["The event \"{0}\" starts in {1} hr."] = "Wydarzenie „{0}” zaczyna się za {1} godz.",
        ["The event \"{0}\" starts in {1} min."] = "Wydarzenie „{0}” zaczyna się za {1} min.",
        ["Expiring soon"] = "Niedługo się przeterminuje",
        ["\"{0}\" is nearing its expiry date ({1})."] = "„{0}” zbliża się do daty ważności ({1}).",
        ["Task reminder"] = "Przypomnienie o zadaniu",
        ["Task \"{0}\" from list \"{1}\" is still waiting to be done."] = "Zadanie „{0}” z listy „{1}” wciąż czeka na wykonanie.",
        ["Overdue task"] = "Zaległe zadanie",
        ["Task \"{0}\" from list \"{1}\" is overdue."] = "Zadanie „{0}” z listy „{1}” jest zaległe.",
        ["Added to a group"] = "Dodano Cię do grupy",
        ["{0} added you to {1}"] = "{0} dodał(a) Cię do grupy {1}",
        // One sentence per kind rather than a noun dropped into a shared one: Polish declines what was
        // shared, so "udostępnił(a) Ci notatkę" and "…listę zadań" cannot come from the same template.
        ["{0} shared a note with you"] = "{0} udostępnił(a) Ci notatkę",
        ["{0} shared a task list with you"] = "{0} udostępnił(a) Ci listę zadań",
        ["{0} shared an event with you"] = "{0} udostępnił(a) Ci wydarzenie",
        ["{0} shared a warehouse with you"] = "{0} udostępnił(a) Ci magazyn",
        ["{0} shared their location with you"] = "{0} udostępnił(a) Ci swoje położenie",
        ["All day"] = "Cały dzień",
        ["Shared by"] = "Udostępnił",
        ["Messages"] = "Wiadomości",
        ["Conversations"] = "Rozmowy",
        ["Daily"] = "Codziennie",
        ["Weekly"] = "Co tydzień",
        ["Monthly"] = "Co miesiąc",
        ["Frequency"] = "Częstotliwość",
        ["It stops repeating before it starts."] = "Powtarzanie kończy się, zanim się zacznie.",
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
        ["Back to inventory"] = "Wróć do magazynów",
        ["Back to task lists"] = "Wróć do list zadań",
        ["This task list no longer exists."] = "Ta lista zadań już nie istnieje.",
        ["This task list was shared by"] = "Tę listę zadań udostępnił",
        ["Completed - follows the linked list, can't be set by hand"] =
            "Ukończone — wynika z powiązanej listy, nie da się ustawić ręcznie",
        ["Completion follows the linked list, so it can't be ticked by hand."] =
            "Ukończenie wynika z powiązanej listy, więc nie da się go odhaczyć ręcznie.",
        ["Sorts this list against the others. Its progress is worked out from its items."] = "Porządkuje tę listę względem pozostałych. Postęp wynika z jej pozycji.",
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
        ["No groups yet."] = "Nie masz jeszcze żadnej grupy.",
        ["Group name"] = "Nazwa grupy",
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
            "Reset wymaga potwierdzonego adresu e-mail, a Twój nie jest jeszcze potwierdzony. Potwierdź go w sekcji",
        ["The one location you've recorded for yourself, and where it is."] =
            "Jedyne położenie, jakie dla siebie zapisałeś, i gdzie ono jest.",
        ["Nothing in it yet."] = "Na razie nic w tym nie ma.",
        ["You'll get your own read-only copy. Nothing you do changes theirs."] =
            "Dostaniesz własną kopię tylko do odczytu. Nic, co zrobisz, nie zmieni oryginału.",
        ["Frontend error"] = "Błąd interfejsu",
        ["Account"] = "Konto",
        // Read aloud in place of the avatar, which is otherwise a pair of initials with no name.
        ["Account menu"] = "Menu konta",
        ["Your account, appearance, and notification preferences."] = "Twoje konto, wygląd i ustawienia powiadomień.",
        ["Your profile, sign-in address, and password."] = "Twój profil, adres logowania i hasło.",
        ["Save profile"] = "Zapisz profil",
        ["Change password"] = "Zmień hasło",
        ["Email verification"] = "Potwierdzenie adresu e-mail",
        ["Delete account"] = "Usuń konto",
        ["Your account has been deleted."] = "Twoje konto zostało usunięte.",
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
        ["Kept on this device, because a browser grants location per device."] = "Zapamiętywane na tym urządzeniu, bo przeglądarka przyznaje dostęp do położenia osobno na każdym.",
        ["Everything you own — notes, task lists, events and storages — as one JSON file, and back again."] =
            "Wszystko, co masz — notatki, listy zadań, wydarzenia i magazyny — w jednym pliku JSON i z powrotem.",
        ["Master switch for everything below, and for the notifications panel itself."] = "Główny przełącznik dla wszystkiego poniżej i dla samego panelu powiadomień.",
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
        ["No user found with that email address or login."] =
            "Nie znaleziono użytkownika o takim adresie e-mail ani loginie.",
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
        ["That file is too large to import."] = "Ten plik jest za duży do zaimportowania.",
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
        ["Orbit — Reset your password"] = "Orbit — Resetowanie hasła",
        ["Forgot your password?"] = "Nie pamiętasz hasła?",
        ["Back to logging in"] = "Wróć do logowania",
        ["Tell us the address or login you sign in with, and we'll email a code to reset the password."] =
            "Podaj adres lub login, którym się logujesz, a wyślemy mailem kod do zresetowania hasła.",
        ["If that account exists and its address is confirmed, a code is on its way. It is good for 15 minutes."] =
            "Jeśli takie konto istnieje i ma potwierdzony adres, kod jest już w drodze. Jest ważny przez 15 minut.",
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
        ["Tick items off; use Edit to change the list itself."] = "Odhaczaj pozycje; użyj Edytuj, aby zmienić samą listę.",
        ["A group list's checklist also shows every list it gathers, so the whole group fits on one screen."] = "Widok listy grupowej pokazuje też wszystkie listy, które zbiera, więc cała grupa mieści się na jednym ekranie.",
        ["Encrypted in this browser, so Orbit can't read it. It can't be shared, and losing your password loses it."] = "Szyfrowana w tej przeglądarce, więc Orbit jej nie odczyta. Nie da się jej udostępnić, a utrata hasła oznacza utratę notatki.",
        ["Encrypted in this browser, so Orbit can't read it. It can't be shared, gets no reminders, and losing your password loses it."] = "Szyfrowana w tej przeglądarce, więc Orbit jej nie odczyta. Nie da się jej udostępnić, nie dostaje przypomnień, a utrata hasła oznacza utratę listy.",
        ["Encrypted in this browser, so Orbit can't read it. It can't be shared, raises no restock or expiry reminders, and losing your password loses it."] = "Szyfrowany w tej przeglądarce, więc Orbit go nie odczyta. Nie da się go udostępnić, nie tworzy zadań uzupełnienia ani przypomnień o terminach, a utrata hasła oznacza utratę magazynu.",
        ["Chat is end-to-end encrypted and your key is protected with a password. Your Google account has none yet, so pick one now."] = "Rozmowy są szyfrowane end-to-end, a klucz chroni hasło. Twoje konto Google jeszcze go nie ma, więc ustaw je teraz.",
        ["This browser has no copy of your encryption key. Enter your password to restore it - it never leaves this browser."] = "Ta przeglądarka nie ma kopii Twojego klucza szyfrującego. Wpisz hasło, aby go odtworzyć — klucz nie opuszcza tej przeglądarki.",
        ["A new password starts your chat over: messages under the old one stay unreadable, because Orbit never had their key."] = "Nowe hasło zaczyna rozmowy od nowa: wiadomości spod starego pozostaną nieczytelne, bo Orbit nigdy nie miał do nich klucza.",
        ["Chat requires a secure connection (an address starting with \"https://\", or \"http://localhost\"). Open Orbit at such an address and try again."] =
            "Rozmowy wymagają bezpiecznego połączenia (adres zaczynający się od „https://” albo „http://localhost”). Otwórz Orbita pod takim adresem i spróbuj ponownie.",
        ["Nothing recorded yet. Orbit asks your browser for your position only when you press the button, and keeps one point with no history."] = "Nic jeszcze nie zapisano. Orbit pyta przeglądarkę o Twoje położenie dopiero po naciśnięciu przycisku i trzyma jeden punkt, bez historii.",
        ["Your position goes out every minute while this page is open. Ending a share deletes it from Orbit."] = "Twoje położenie wysyłane jest co minutę, dopóki ta strona jest otwarta. Zakończenie udostępniania usuwa je z Orbita.",
        ["Anyone with this link can read it without an account. They can't change it, and they can save their own read-only copy by signing in."] =
            "Każdy, kto ma ten link, może to przeczytać bez konta. Nie może tego zmienić, a po zalogowaniu może zapisać u siebie własną kopię tylko do odczytu.",
        ["It may have been turned off by whoever shared it, or the thing it pointed at may be gone. Ask them for a new one."] =
            "Osoba, która go udostępniła, mogła go wyłączyć, albo rzecz, na którą wskazywał, już nie istnieje. Poproś o nowy.",
        ["The invitation always reaches your notifications. This adds a push or email on top, so you hear about it straight away."] = "Zaproszenie i tak zawsze trafia do powiadomień. To dokłada push lub e-mail, żebyś dowiedział się od razu.",
        ["Downloads everything in your account as one file. Things shared with you are left out, and a private item travels sealed."] = "Pobiera wszystko z Twojego konta jako jeden plik. Rzeczy udostępnione Tobie zostają pominięte, a element prywatny podróżuje zaszyfrowany.",
        ["Downloads what you tick below as one file. Things shared with you are left out, and a private item travels sealed."] = "Pobiera jako jeden plik to, co zaznaczysz poniżej. Rzeczy udostępnione Tobie zostają pominięte, a element prywatny podróżuje zaszyfrowany.",
        ["Adds everything in a file to this account. Nothing is replaced, so importing the same file twice leaves two copies."] = "Dodaje do tego konta wszystko z pliku. Nic nie jest zastępowane, więc dwukrotny import zostawia dwie kopie.",
        ["Lets the Notifications panel list this browser's own recent errors, each with a \"Copy\" button for reporting a bug."] =
            "Pozwala panelowi powiadomień wypisać ostatnie błędy tej przeglądarki, każdy z przyciskiem „Kopiuj” do zgłoszenia usterki.",
        ["Lets Orbit ask this browser where you are. Until you turn it on nothing asks, and turning it on sends nothing anywhere."] = "Pozwala Orbitowi zapytać tę przeglądarkę, gdzie jesteś. Dopóki tego nie włączysz, nic nie pyta — a samo włączenie niczego nie wysyła.",
        ["Debug shows the captured log and the detail behind an error. Release hides both."] = "Debug pokazuje zapisany log i szczegóły błędu. Release chowa jedno i drugie.",
        ["The least severe line this browser keeps. Warning by default, so routine noise doesn't crowd out a real failure."] = "Najłagodniejszy wpis, jaki ta przeglądarka zachowuje. Domyślnie Warning, żeby rutynowy szum nie wypierał prawdziwej awarii.",
        ["before it is deleted for good (1-90). Clearing the panel only tidies them out of the way; this is what actually removes them."] =
            "zanim zostanie usunięte na dobre (1–90). Wyczyszczenie panelu tylko je uprząta; to ustawienie faktycznie je kasuje.",
        ["Permanently deletes your account and everything in it - notes, tasks, calendar events, inventory, and chat history. This cannot be undone."] =
            "Trwale usuwa Twoje konto i wszystko, co się w nim znajduje — notatki, zadania, wydarzenia, magazyn i historię rozmów. Tego nie da się cofnąć.",
        ["Saving a different address doesn't move the account to it on its own - that only happens once you confirm the code sent to the new address."] =
            "Zapisanie innego adresu samo w sobie nie przenosi na niego konta — dzieje się to dopiero po potwierdzeniu kodu wysłanego na nowy adres.",
        ["Adds links that open Google Calendar or Google Maps with the details filled in. Orbit never writes to your calendar."] = "Dodaje linki otwierające Kalendarz Google lub Mapy Google z wypełnionymi danymi. Orbit sam nic nie zapisuje w Twoim kalendarzu.",

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
        ["The map can't be shown in this build."] = "Ta wersja aplikacji nie potrafi pokazać mapy.",
        ["Unreadable - encrypted with an older key"] = "Nieczytelne — zaszyfrowane starszym kluczem",
        ["another list"] = "inną listą",
        ["another user"] = "inny użytkownik",
        ["This login is already taken."] = "Ten login jest już zajęty.",
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
        ["Everything from the last {0}, newest first — cleared ones included. Older than that and they are deleted; change how long in"] = "Wszystko z ostatnich {0}, od najnowszych — łącznie z wyczyszczonymi. Starsze są usuwane; długość zmienisz w sekcji",
        ["day"] = "dzień",
        ["Group list - {0} linked {1} shown below."] = "Lista grupowa — poniżej powiązane listy ({0}).",
        // The phone's switch that turns a list into a group list needs a name of its own; the web only
        // ever says it inside the sentence above, which is too long for a label beside a switch.
        ["Group list"] = "Lista grupowa",
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
        ["Too many attempts. Wait a minute and try again."] = "Zbyt wiele prób. Odczekaj minutę i spróbuj ponownie.",
        ["{0} new"] = "nowe: {0}",
        ["When it starts"] = "Gdy się zaczyna",
        ["Shared their location, and keeps it up to date."] = "Udostępnił swoje położenie i na bieżąco je odświeża.",
        ["Shared where they are right now."] = "Udostępnił, gdzie właśnie jest.",
        ["Shared with you"] = "Udostępnione Tobie",
        ["Due date"] = "Termin",
        ["Due time"] = "Godzina",
        ["That message is no longer yours to edit."] = "Tej wiadomości nie możesz już edytować.",
        ["Nothing matches that."] = "Nic nie pasuje.",
        ["Leave group"] = "Opuść grupę",
        ["You aren't in this group."] = "Nie należysz do tej grupy.",
        ["Remove from my list"] = "Usuń z mojej listy",
        ["Remove \"{0}\" from your list? The owner keeps it."] = "Usunąć „{0}” z Twojej listy? Właściciel ją zachowa.",
        ["Delete note \"{0}\"?"] = "Usunąć notatkę „{0}”?",

        // ---- Orbit.Maui's own wording. "Orbit" and the bare glyphs are deliberately absent:
        // a product's name and a + are the same in both languages. ----
        ["Add someone"] = "Dodaj osobę",
        ["Back to account"] = "Wróć do konta",
        ["Back to dashboard"] = "Wróć do pulpitu",
        ["Back to groups"] = "Wróć do grup",
        ["Change username"] = "Zmień nazwę użytkownika",
        ["Changing your username, email address or password needs a connection to Orbit. None of it is saved to send later."] =
            "Zmiana nazwy użytkownika, adresu e-mail lub hasła wymaga połączenia z Orbitem. Nic z tego nie zostanie zapisane na później.",
        ["Chat is unlocked"] = "Rozmowy są odblokowane",
        ["Chat key"] = "Klucz rozmów",
        ["Confirm new address"] = "Potwierdź nowy adres",
        ["Confirmation code"] = "Kod potwierdzający",
        ["Create account"] = "Załóż konto",
        ["Create an account"] = "Załóż konto",
        ["Creating an account needs a connection to Orbit."] = "Założenie konta wymaga połączenia z Orbitem.",
        ["Delete list"] = "Usuń listę",
        ["Find"] = "Szukaj",
        ["Forward to"] = "Przekaż do",
        ["I already have an account"] = "Mam już konto",
        ["It always appears in your notifications. This also pushes or emails."] =
            "Zawsze pojawi się w powiadomieniach. To dodatkowo wyśle powiadomienie push lub e-mail.",
        ["Mark all read"] = "Oznacz wszystkie jako przeczytane",
        ["Message"] = "Wiadomość",
        ["New email address"] = "Nowy adres e-mail",
        ["New item"] = "Nowa pozycja",
        ["New list"] = "Nowa lista",
        ["New username"] = "Nowa nazwa użytkownika",
        ["New warehouse"] = "Nowy magazyn",
        ["No messages yet."] = "Nie ma jeszcze wiadomości.",
        ["No notes yet."] = "Nie ma jeszcze notatek.",
        ["No warehouses yet."] = "Nie ma jeszcze magazynów.",
        ["Nobody is sharing their position with you."] = "Nikt nie udostępnia Ci swojego położenia.",
        ["Nobody."] = "Nikt.",
        ["Nothing coming up."] = "Nic się nie zbliża.",
        ["Nothing has been logged yet."] = "Nic jeszcze nie zostało zapisane.",
        ["Nothing here yet."] = "Nic tu jeszcze nie ma.",
        ["Nothing here yet. Add a note or a task to get started."] =
            "Nic tu jeszcze nie ma. Dodaj notatkę albo zadanie, żeby zacząć.",
        ["Nothing in this warehouse yet."] = "W tym magazynie nic jeszcze nie ma.",
        ["Nothing on this list yet."] = "Na tej liście nic jeszcze nie ma.",
        ["Notification settings"] = "Ustawienia powiadomień",
        ["Notify me"] = "Powiadamiaj mnie",
        ["Off means nothing is recorded or sent at all."] =
            "Wyłączone oznacza, że nic nie jest zapisywane ani wysyłane.",
        ["Push to this phone"] = "Powiadomienia na ten telefon",
        ["Read"] = "Przeczytane",
        ["Read my position"] = "Odczytaj moje położenie",
        ["Record everything"] = "Zapisuj wszystko",
        ["Remove admin"] = "Odbierz uprawnienia administratora",
        ["Send confirmation code"] = "Wyślij kod potwierdzający",
        ["Send to Orbit"] = "Wyślij do Orbita",
        ["Set password and start chat over"] = "Ustaw hasło i zacznij rozmowy od nowa",
        ["Settings"] = "Ustawienia",
        ["Share it"] = "Udostępnij",
        ["Share with"] = "Udostępnij osobie",
        ["Sign in"] = "Zaloguj się",
        ["Sign in with Google"] = "Zaloguj się przez Google",
        ["Google couldn't be used to sign in to Orbit."] = "Nie udało się zalogować do Orbita przez Google.",
        ["Sign out"] = "Wyloguj się",
        ["This device now holds your encryption key."] = "To urządzenie ma teraz Twój klucz szyfrujący.",
        ["Update Orbit"] = "Zaktualizuj Orbita",
        ["Waiting for them to accept your request"] = "Czeka na akceptację zaproszenia",
        ["Waiting to send"] = "Czeka na wysłanie",
        ["Wants to chat with you"] = "Chce z Tobą porozmawiać",
        ["Warnings and errors otherwise. Goes back to that on the next launch."] =
            "W przeciwnym razie tylko ostrzeżenia i błędy. Po następnym uruchomieniu wraca do tego ustawienia.",
        ["When something is shared with me"] = "Gdy ktoś mi coś udostępni",
        ["Where you are"] = "Gdzie jesteś",
        ["Who can see you"] = "Kto Cię widzi",
        ["Who is in it"] = "Kto należy do grupy",
        ["Couldn't sync"] = "Nie udało się zsynchronizować",
        ["Just now"] = "Przed chwilą",
        ["No connection"] = "Bez połączenia",
        ["Synced"] = "Zsynchronizowano",
        ["Syncing…"] = "Synchronizowanie…",
        ["Unavailable"] = "Niedostępny",
        ["Untitled event"] = "Wydarzenie bez tytułu",
        ["Untitled list"] = "Lista bez tytułu",
        ["Wants to chat"] = "Chce porozmawiać",
        ["{0}d ago"] = "{0} dni temu",
        ["Finding somebody new needs a connection."] = "Znalezienie nowej osoby wymaga połączenia.",
        ["No conversations yet."] = "Nie ma jeszcze rozmów.",
        ["Nobody has that email address or username. It has to match exactly."] =
            "Nikt nie ma takiego adresu e-mail ani nazwy użytkownika. Musi się zgadzać dokładnie.",
        ["{0} hasn't set up chat yet, so there is nothing to encrypt a message for. They need to open Orbit's chat once, on any device."] =
            "{0} nie ma jeszcze skonfigurowanych rozmów, więc nie ma czym zaszyfrować wiadomości. Musi raz otworzyć rozmowy w Orbicie, na dowolnym urządzeniu.",
        ["Not read yet."] = "Jeszcze nie odczytano.",
        ["Signed in as {0}"] = "Zalogowano jako {0}",
        // ---- Everything the phone's screens say back to the reader: what a change did, why one
        // was refused, and the short lines under a row. Composed in code, so the XAML sweep that
        // came first could not see any of it. ----
        ["Waiting to sync"] = "Czeka na synchronizację",
        ["{0} is editing this right now - it stays read-only until they finish."]
            = "{0} właśnie to edytuje - pozostaje tylko do odczytu, dopóki nie skończy.",
        ["Who you are to Orbit, and what this device is allowed to do."]
            = "Kim jesteś dla Orbita i co wolno temu urządzeniu.",
        ["Already accepted"] = "Już przyjęto",
        ["Item options"] = "Opcje pozycji",
        ["{0} minutes before"] = "{0} min przed",
        ["List options"] = "Opcje listy",
        ["Built a warehouse from what this list needs."] = "Zbudowano magazyn z tego, czego potrzebuje ta lista.",
        ["There was nothing on this list to build a warehouse from."]
            = "Na tej liście nie było nic, z czego można zbudować magazyn.",
        ["Added {0} to the restock list."] = "Dodano {0} do listy uzupełnień.",
        ["Nothing new to add - what is short is already waiting there."]
            = "Nie ma nic nowego do dodania - brakujące już tam czekają.",
        ["Couldn't work out what this needs without a connection."]
            = "Bez połączenia nie da się ustalić, czego to wymaga.",
        ["Invite"] = "Zaproś",
        ["Somebody else"] = "Ktoś inny",
        ["Name of the place"] = "Nazwa miejsca",
        ["Couldn't work out where this phone is."] = "Nie udało się ustalić, gdzie jest ten telefon.",
        ["Delete warehouse"] = "Usuń magazyn",
        ["Delete item"] = "Usuń pozycję",
        ["Line options"] = "Opcje wiersza",
        ["Make it a checklist item"] = "Zmień w pozycję listy",
        ["Make it an ordinary line"] = "Zmień w zwykły wiersz",
        ["Delete line"] = "Usuń wiersz",
        ["Everything Orbit has told you, newest first."] = "Wszystko, co Orbit Ci powiedział, od najnowszych.",
        ["Moved to {0}."] = "Przeniesiono do: {0}.",
        ["Couldn't move it. Try again."] = "Nie udało się przenieść. Spróbuj ponownie.",
        ["That move isn't allowed."] = "Takie przeniesienie nie jest dozwolone.",
        ["Shared with you - read-only until you're back online"] =
            "Udostępnione Tobie — tylko do odczytu, dopóki nie wrócisz online",
        ["Shared with others - read-only until you're back online"] =
            "Udostępnione innym — tylko do odczytu, dopóki nie wrócisz online",
        ["Saved on this phone - it will sync later"] = "Zapisano na tym telefonie — zsynchronizuje się później",
        ["Somebody else can change this warehouse, and Orbit can't be reached to check. It stays read-only until you're back online."] =
            "Ktoś inny może zmieniać ten magazyn, a Orbit jest poza zasięgiem i nie da się tego sprawdzić. Zostaje tylko do odczytu, dopóki nie wrócisz online.",

        // Counted things put the label first and the number after it. Polish declines the noun
        // after a numeral - one, two and five each take a different form - so "{0} items" has no
        // single correct translation, while "Pozycji: {0}" is right for every count.
        ["Items: {0}"] = "Pozycji: {0}",
        ["Done: {0} of {1}"] = "Ukończone: {0} z {1}",
        ["No items yet"] = "Nie ma jeszcze pozycji",
        ["People"] = "Osób",
        ["Tasks due today"] = "Zadania na dziś",
        ["Events today"] = "Wydarzenia dziś",
        ["New chat requests"] = "Nowe zaproszenia do rozmowy",
        ["Sent. Entries: {0}. Thank you."] = "Wysłano. Wpisów: {0}. Dziękujemy.",

        ["Updated {0}"] = "Zmieniono {0}",
        ["{0} · all day"] = "{0} · cały dzień",
        ["{0} – {1}"] = "{0} – {1}",
        ["Untitled"] = "Bez tytułu",
        ["Someone"] = "Ktoś",
        ["Live · updated {0}"] = "Na żywo · zaktualizowano {0}",
        ["One-off · shared {0}"] = "Jednorazowo · udostępniono {0}",

        // Chat: sending, editing and forwarding.
        ["Offline - your message is saved and will send later"] =
            "Bez połączenia — wiadomość jest zapisana i wyśle się później",
        ["Offline - showing what's on this phone"] = "Bez połączenia — pokazujemy to, co jest na telefonie",
        ["Offline, and this device hasn't seen your conversations yet."] =
            "Bez połączenia, a to urządzenie nie widziało jeszcze Twoich rozmów.",
        ["Offline, and this device hasn't seen your groups yet."] =
            "Bez połączenia, a to urządzenie nie widziało jeszcze Twoich grup.",
        ["Couldn't refresh just now"] = "Nie udało się teraz odświeżyć",
        ["Couldn't sync this conversation just now"] = "Nie udało się teraz zsynchronizować tej rozmowy",
        ["This person hasn't set up chat yet."] = "Ta osoba nie ma jeszcze skonfigurowanych rozmów.",
        ["Accepting a chat request needs a connection."] = "Przyjęcie zaproszenia do rozmowy wymaga połączenia.",
        ["Accept their chat request first - your message wasn't sent."] =
            "Najpierw przyjmij zaproszenie do rozmowy — wiadomość nie została wysłana.",
        ["Somebody here hasn't set up chat yet, so this couldn't be encrypted."] =
            "Ktoś tutaj nie ma jeszcze skonfigurowanych rozmów, więc nie dało się tego zaszyfrować.",
        ["Somebody here hasn't set up chat, so it couldn't be re-encrypted."] =
            "Ktoś tutaj nie ma skonfigurowanych rozmów, więc nie dało się tego zaszyfrować ponownie.",
        ["This conversation is no longer available - your message wasn't sent."] =
            "Ta rozmowa nie jest już dostępna — wiadomość nie została wysłana.",
        ["Your message couldn't be sent."] = "Nie udało się wysłać wiadomości.",
        ["Changing a message needs a connection."] = "Zmiana wiadomości wymaga połączenia.",
        ["That message can't be changed any more."] = "Tej wiadomości nie można już zmienić.",
        ["No other conversations to forward this to yet."] = "Nie ma jeszcze innych rozmów, do których można to przekazać.",
        ["Forwarded to {0}."] = "Przekazano do: {0}.",
        ["Forwarded from"] = "Przekazane od",

        // Groups.
        ["Give the group a name."] = "Nadaj grupie nazwę.",
        ["Pick at least one person."] = "Wybierz przynajmniej jedną osobę.",
        ["You have nobody to add yet - start a conversation first."] =
            "Nie masz jeszcze kogo dodać — zacznij najpierw rozmowę.",
        ["Everybody you have a conversation with is already in this group."] =
            "Wszyscy, z którymi rozmawiasz, są już w tej grupie.",
        ["Creating a group needs a connection."] = "Utworzenie grupy wymaga połączenia.",
        ["Changing who is in a group needs a connection."] = "Zmiana składu grupy wymaga połączenia.",
        ["This group is no longer available."] = "Ta grupa nie jest już dostępna.",

        // Signing in, the account screen and the chat key.
        ["Couldn't reach Orbit. Check your connection and try again."] =
            "Nie udało się połączyć z Orbitem. Sprawdź połączenie i spróbuj ponownie.",
        ["Those details weren't recognised."] = "Nie rozpoznano tych danych.",
        ["Couldn't create that account."] = "Nie udało się założyć tego konta.",
        ["Couldn't load your account. Check your connection and try again."] =
            "Nie udało się wczytać Twojego konta. Sprawdź połączenie i spróbuj ponownie.",
        ["Couldn't set that password."] = "Nie udało się ustawić tego hasła.",
        ["Couldn't send a reset code."] = "Nie udało się wysłać kodu resetu.",
        ["We will email a code to {0}."] = "Wyślemy kod na adres {0}.",
        ["Couldn't unlock your chat key. Either that isn't the password it was saved under, or Orbit couldn't be reached. Nothing was changed."] =
            "Nie udało się odblokować klucza rozmów. Albo to nie jest hasło, pod którym go zapisano, albo Orbit był poza zasięgiem. Nic nie zostało zmienione.",
        ["Password changed, but your chat key backup couldn't be updated. Open \"Chat key\" to fix it, or older messages may not open on a new device."] =
            "Hasło zmienione, ale nie udało się zaktualizować kopii klucza rozmów. Otwórz „Klucz rozmów”, aby to naprawić — inaczej starsze wiadomości mogą się nie otworzyć na nowym urządzeniu.",
        ["Password changed, but your chat key backup couldn't be updated. Sign in again while online to fix it."] =
            "Hasło zmienione, ale nie udało się zaktualizować kopii klucza rozmów. Zaloguj się ponownie z połączeniem, aby to naprawić.",

        // Notifications and diagnostics.
        ["Show all"] = "Pokaż wszystkie",
        ["Recent only"] = "Tylko ostatnie",
        ["This notification points somewhere this version of Orbit doesn't have. Updating should fix it."] =
            "To powiadomienie prowadzi w miejsce, którego ta wersja Orbita nie zna. Aktualizacja powinna to naprawić.",
        ["Couldn't find what this is about on this phone. It may need a connection to catch up first."] =
            "Nie znaleziono na tym telefonie tego, czego dotyczy. Może najpierw potrzebować połączenia, żeby nadrobić zaległości.",
        ["There is nothing to send yet."] = "Nie ma jeszcze czego wysłać.",
        ["Sent, but nothing in the log could be read."] = "Wysłano, ale nie udało się odczytać nic z dziennika.",
        ["Couldn't send it - Orbit is out of reach."] = "Nie udało się wysłać — Orbit jest poza zasięgiem.",
        ["Orbit wouldn't accept the log. Try signing in again."] =
            "Orbit nie przyjął dziennika. Spróbuj zalogować się ponownie.",
        ["{0} - Orbit is out of reach."] = "{0} — Orbit jest poza zasięgiem.",
        ["{0}. Try signing in again."] = "{0}. Spróbuj zalogować się ponownie.",

        // The forced-update gate.
        ["This version of Orbit is no longer supported. Update to continue."] =
            "Ta wersja Orbita nie jest już wspierana. Zaktualizuj, aby kontynuować.",
        ["This version of Orbit is no longer supported. Update to {0} to continue."] =
            "Ta wersja Orbita nie jest już wspierana. Zaktualizuj do {0}, aby kontynuować.",
        // The longer explanations on Orbit.Maui's own screens. They were written straight into the
        // markup rather than asked of the dictionary, which is why the first sweep walked past them.
        ["Chat is end-to-end encrypted and your key is protected with a password. This account signs in with Google and doesn't have one yet, so pick one now - it's also what lets you read your messages on another device."] =
            "Rozmowy są szyfrowane end-to-end, a klucz chroni hasło. To konto loguje się przez Google i jeszcze go nie ma, więc ustaw je teraz — to również ono pozwala czytać wiadomości na innym urządzeniu.",
        ["This device doesn't have a copy of your encryption key yet. Enter your password to restore it from your encrypted backup - it never leaves this device."] =
            "To urządzenie nie ma jeszcze kopii Twojego klucza szyfrującego. Wpisz hasło, aby odtworzyć go z zaszyfrowanej kopii — klucz nie opuszcza tego urządzenia.",
        ["Setting a new password starts your chat over: messages encrypted under the old one stay unreadable, because Orbit's servers never had the key to them."] =
            "Nowe hasło zaczyna rozmowy od nowa: wiadomości zaszyfrowane starym pozostaną nieczytelne, bo serwery Orbita nigdy nie miały do nich klucza.",
        ["This message can't be opened on this device."] = "Tej wiadomości nie da się otworzyć na tym urządzeniu.",
        ["This position can't be opened on this device."] = "Tej lokalizacji nie da się otworzyć na tym urządzeniu.",
        ["Saved."] = "Zapisano.",
        ["Pinning needs a connection."] = "Przypięcie wymaga połączenia.",

        // The note editor - the screen a note opens into.
        ["New line"] = "Nowy wiersz",
        ["Nothing written here yet."] = "Nic tu jeszcze nie napisano.",
        ["Back to notes"] = "Wróć do notatek",
        ["Delete note"] = "Usuń notatkę",

        // A warehouse item's own details, which the phone could neither show nor set.
        ["Quantity"] = "Ilość",

        // Somebody offering to share something, which arrives in a conversation.
        ["Shared something with you"] = "Udostępnił(a) Ci coś",
        ["Read only"] = "Tylko do odczytu",

        // A link anyone can read the thing by, with no Orbit account and nothing to accept.
        ["Share a link"] = "Udostępnij linkiem",

        // Asking whoever owns something to let you change it, and seeing that ask arrive.
        ["Asked to edit"] = "Prosi o prawo edycji",
        ["Asked them. They will see it in your conversation."] = "Poproszono. Zobaczy to w Waszej rozmowie.",
        ["Couldn't send that request."] = "Nie udało się wysłać tej prośby.",
        ["Stop the link"] = "Wyłącz link",
        ["That link no longer works."] = "Ten link już nie działa.",
        ["Couldn't make a link for that."] = "Nie udało się utworzyć linku do tego.",
        ["This Orbit doesn't have a web address set, so a link can't be built."] =
            "Ten Orbit nie ma ustawionego adresu wersji webowej, więc nie da się zbudować linku.",
        ["Sharing needs a connection."] = "Udostępnianie wymaga połączenia.",
        ["Couldn't share that."] = "Nie udało się tego udostępnić.",
        ["{0} is yours now."] = "{0} — już Twoje.",
        ["That offer is no longer available."] = "Ta oferta nie jest już dostępna.",
        ["Accepting what somebody shared needs a connection."] = "Przyjęcie udostępnionej rzeczy wymaga połączenia.",

        // A task-list entry's own details, and the filters over the lists themselves.
        ["New"] = "Nowe",
        ["Pending"] = "W toku",
        ["Due {0}"] = "Termin: {0}",
        ["Daily at {0}"] = "Codziennie o {0}",
        ["When it is overdue"] = "Gdy termin minie",
        ["Remind me daily"] = "Przypominaj codziennie",
        ["Minimum"] = "Minimum",
        ["Minimum: {0}"] = "Minimum: {0}",
        ["Expires {0}"] = "Termin: {0}",

        // The event editor - the screen a calendar entry opens into.
        ["Back to calendar"] = "Wróć do kalendarza",
        ["Somebody else can change this event, and Orbit can't be reached to check. It stays read-only until you're back online."] =
            "Ktoś inny może zmieniać to wydarzenie, a Orbit jest poza zasięgiem i nie da się tego sprawdzić. Zostaje tylko do odczytu, dopóki nie wrócisz online.",
        ["Somebody else can change this note, and Orbit can't be reached to check. It stays read-only until you're back online."] =
            "Ktoś inny może zmieniać tę notatkę, a Orbit jest poza zasięgiem i nie da się tego sprawdzić. Zostaje tylko do odczytu, dopóki nie wrócisz online.",
        ["This note is private, and its words are sealed with a key this phone doesn't have."] =
            "Ta notatka jest prywatna, a jej treść jest zapieczętowana kluczem, którego ten telefon nie ma.",
        ["No earlier messages could be passed on - this device can't open any of them."] =
            "Nie udało się przekazać żadnej wcześniejszej wiadomości — to urządzenie nie potrafi otworzyć ani jednej.",
        ["Passed on {0} earlier messages."] =
            "Przekazano wcześniejsze wiadomości: {0}.",
        ["They're in the group, but passing on the earlier messages didn't work."] =
            "Są już w grupie, ale przekazanie wcześniejszych wiadomości się nie udało.",
        ["They're in the group, but the earlier messages couldn't be passed on until they've signed in once."] =
            "Są już w grupie, ale wcześniejszych wiadomości nie da się przekazać, dopóki choć raz się nie zalogują.",
        ["{0} joined {1}"] = "{0} dołączył(a) do grupy {1}",
        ["{0} shared the conversation so far"] = "{0} udostępnił(a) dotychczasową rozmowę",
        ["Somebody"] = "Ktoś",
        ["A daily reminder needs a time to arrive at."] =
            "Codzienne przypomnienie potrzebuje godziny, o której ma przyjść.",
        ["Choose a time"] = "Wybierz godzinę",
        ["The name is yours to write - the point is kept either way."] =
            "Nazwa należy do Ciebie — punkt i tak zostaje zapisany.",
        ["Refresh the restock list"] = "Odśwież listę uzupełnień",
        ["Share the conversation so far"] = "Udostępnij dotychczasową rozmowę",
        ["They will be able to read what was said before they joined."] = "Będą mogli przeczytać to, co napisano przed ich dołączeniem.",
        ["They're in the group, but this device has no key to open the earlier messages with."] = "Są już w grupie, ale to urządzenie nie ma klucza, którym można otworzyć wcześniejsze wiadomości.",
        ["Use this name"] = "Użyj tej nazwy",
        ["Only you can read it, and it cannot be shared."] =
            "Tylko Ty możesz to przeczytać i nie da się tego udostępnić.",
        ["This note is private. Unlock this device's encryption key to read it."] =
            "Ta notatka jest prywatna. Odblokuj klucz szyfrowania na tym urządzeniu, żeby ją przeczytać.",
        ["This note was sealed with an encryption key this account no longer has."] =
            "Ta notatka została zapieczętowana kluczem szyfrowania, którego to konto już nie ma.",
        ["This list is private, and its contents are sealed with a key this phone doesn't have."] =
            "Ta lista jest prywatna, a jej zawartość jest zapieczętowana kluczem, którego ten telefon nie ma.",
        ["This list is private. Unlock this device's encryption key to read it."] =
            "Ta lista jest prywatna. Odblokuj klucz szyfrowania na tym urządzeniu, żeby ją przeczytać.",
        ["This list was sealed with an encryption key this account no longer has."] =
            "Ta lista została zapieczętowana kluczem szyfrowania, którego to konto już nie ma.",
        ["This warehouse is private, and its contents are sealed with a key this phone doesn't have."] =
            "Ten magazyn jest prywatny, a jego zawartość jest zapieczętowana kluczem, którego ten telefon nie ma.",
        ["This warehouse is private. Unlock this device's encryption key to read it."] =
            "Ten magazyn jest prywatny. Odblokuj klucz szyfrowania na tym urządzeniu, żeby go otworzyć.",
        ["This warehouse was sealed with an encryption key this account no longer has."] =
            "Ten magazyn został zapieczętowany kluczem szyfrowania, którego to konto już nie ma.",

        // One group message's own info view: who it reached, and who has opened it.
        ["{0} - read {1}"] = "{0} — przeczytano {1}",
        ["{0} - delivered"] = "{0} — dostarczono",
        ["Nobody else has a copy of this yet."] = "Nikt inny nie ma jeszcze kopii tej wiadomości.",
        ["Couldn't read who has seen this."] = "Nie udało się odczytać, kto to widział.",
        ["Orbit's own log, on this phone. Nothing leaves it unless you send it."] =
            "Własny dziennik Orbita, na tym telefonie. Nic z niego nie wychodzi, dopóki go nie wyślesz.",
        // The map screen: reading a position, sharing it, and stopping.
        ["Couldn't reach Orbit just now."] = "Nie udało się teraz połączyć z Orbitem.",
        ["Orbit needs permission to use your location. Turn it on in Settings."] =
            "Orbit potrzebuje zgody na dostęp do Twojej lokalizacji. Włącz ją w Ustawieniach.",
        ["Couldn't get a position - try again outdoors."] =
            "Nie udało się ustalić położenia — spróbuj ponownie na zewnątrz.",
        ["Read your position, but couldn't save it - Orbit is out of reach."] =
            "Odczytano Twoje położenie, ale nie udało się go zapisać — Orbit jest poza zasięgiem.",
        ["Read your position, but Orbit wouldn't store it. Try signing in again."] =
            "Odczytano Twoje położenie, ale Orbit go nie zapisał. Spróbuj zalogować się ponownie.",
        ["Read your position first."] = "Najpierw odczytaj swoje położenie.",
        ["Nobody to share with yet - start a conversation first."] =
            "Nie masz jeszcze komu udostępnić — zacznij najpierw rozmowę.",
        ["Shared with {0}."] = "Udostępniono: {0}.",
        ["Sharing with {0}. Your position goes out again every minute while this screen is open."] = "Udostępniasz: {0}. Twoje położenie wysyłane jest co minutę, dopóki ten ekran jest otwarty.",
        ["{0} hasn't set up Orbit's encryption yet, so there is nothing to share to."] = "{0} nie ma jeszcze skonfigurowanego szyfrowania Orbita, więc nie ma dokąd tego udostępnić.",
        ["{0} can no longer see where you are."] = "{0} nie widzi już, gdzie jesteś.",
        ["Sharing a position needs a connection."] = "Udostępnienie położenia wymaga połączenia.",
        ["Orbit wouldn't accept that share. Try signing in again."] =
            "Orbit nie przyjął tego udostępnienia. Spróbuj zalogować się ponownie.",
        ["Stopping needs a connection - they can still see you until it goes through."] =
            "Zatrzymanie wymaga połączenia — do tego czasu ta osoba nadal Cię widzi.",
        ["Orbit wouldn't stop that share - they can still see you."] =
            "Orbit nie zatrzymał tego udostępnienia — ta osoba nadal Cię widzi.",

        ["Cleared."] = "Wyczyszczono.",

        ["Couldn't mark them read"] = "Nie udało się oznaczyć ich jako przeczytane",
        ["Couldn't clear them"] = "Nie udało się ich wyczyścić",
        ["Couldn't read your notifications"] = "Nie udało się odczytać Twoich powiadomień",
        ["Couldn't read your notification settings"] = "Nie udało się odczytać ustawień powiadomień",
        ["Couldn't save your notification settings"] = "Nie udało się zapisać ustawień powiadomień",

        // The phone's own way into the permissions the web puts under Options.
        ["Enter the code for it on the account screen, under Permissions."] =
            "Wpisz kod do niego na ekranie konta, w sekcji Uprawnienia.",
        ["{0} is unlocked."] = "{0} — odblokowano.",

        // ---- Orbit.Web's newer screens, merged from main. ----
        ["Full screen"] = "Pełny ekran",
        ["Leave full screen"] = "Zamknij pełny ekran",
        ["Info"] = "Info",
        ["Who has read this"] = "Kto to przeczytał",
        ["Close"] = "Zamknij",
        ["Delivered"] = "Dostarczone",
        ["Read by everyone"] = "Przeczytane przez wszystkich",
        ["Read {0}"] = "Przeczytane {0}",

        // The same page on a phone, where the reader already has the app and the word for it is
        // "update" - see UpdateViewModel. Only the platform being read on is drawn there, so the
        // strings below are shared with the web's page rather than doubled.
        ["Update"] = "Aktualizacja",
        ["Where a newer Orbit comes from, and how to install it."] =
            "Skąd wziąć nowszego Orbita i jak go zainstalować.",
        ["Orbit {0} is out. You have {1}."] = "Jest Orbit {0}. Masz {1}.",
        ["A newer Orbit is out."] = "Jest nowszy Orbit.",
        ["You have Orbit {0}, which is the newest there is."] = "Masz Orbita {0} — nowszego nie ma.",
        ["Orbit hasn't been able to check for a newer version yet."] =
            "Orbit nie zdążył jeszcze sprawdzić, czy jest nowsza wersja.",

        // The page the phone apps are downloaded from.
        ["Orbit — Get the app"] = "Orbit — Pobierz aplikację",
        ["Get the app"] = "Pobierz aplikację",
        ["The phone apps sign in to this same Orbit, with the same account and the same data."] =
            "Aplikacje na telefon logują się do tego samego Orbita, tym samym kontem i z tymi samymi danymi.",
        ["Android"] = "Android",
        ["iPhone"] = "iPhone",
        ["Download for Android"] = "Pobierz na Androida",
        ["Open the downloaded file. Android will ask whether this browser may install apps - allow it."] =
            "Otwórz pobrany plik. Android zapyta, czy ta przeglądarka może instalować aplikacje — zezwól.",
        ["Install, then open Orbit and sign in with your usual account."] =
            "Zainstaluj, otwórz Orbita i zaloguj się swoim zwykłym kontem.",
        ["This build is not from Google Play, so Android checks with you before installing it. That is the prompt about unknown sources, and it is expected here."] =
            "Ta wersja nie pochodzi z Google Play, więc Android pyta o zgodę przed instalacją. To właśnie pytanie o nieznane źródła — tak ma być.",
        ["No Android build has been published yet."] = "Nie opublikowano jeszcze wersji na Androida.",
        ["Open the TestFlight invitation"] = "Otwórz zaproszenie w TestFlight",
        ["Install Apple's TestFlight app, then open the invitation on the iPhone itself."] =
            "Zainstaluj aplikację TestFlight od Apple, a potem otwórz zaproszenie na samym iPhonie.",
        ["Install Orbit from TestFlight and sign in with your usual account."] =
            "Zainstaluj Orbita z TestFlight i zaloguj się swoim zwykłym kontem.",
        ["No iPhone build has been published yet. iOS installs nothing a browser downloaded, so this will be a TestFlight invitation rather than a file."] =
            "Nie opublikowano jeszcze wersji na iPhone'a. iOS nie zainstaluje niczego, co pobrała przeglądarka, więc będzie to zaproszenie w TestFlight, a nie plik.",
        ["Orbit on your phone"] = "Orbit na telefonie",
    };
}
