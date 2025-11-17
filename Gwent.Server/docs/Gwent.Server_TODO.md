
```markdown
# Gwent.Server – TODO / roadmap

## 1. Stabilność i obsługa błędów

- [ ] **Bezpieczne rozłączenia**:
  - [ ] W `HandleClientAsync` rozróżnić:
    - normalne zamknięcie (`bytesRead == 0`),
    - `SocketException` (np. 10054 – „connection reset by peer”),
  - [ ] w obu przypadkach:
    - usunąć klienta z `connectedClients`,
    - poinformować drugiego gracza (`ServerInfo`/`Disconnect` + ustawienie zwycięzcy w `GameBoardState`).
- [ ] **Ochrona przed crashami**:
  - [ ] w `HandleNetworkMessageAsync` mieć globalny try/catch z logiem,
  - [ ] niewłaściwy JSON → odesłać `Error` do klienta zamiast zamykać serwer.
- [ ] **Timeout / keepalive**:
  - [ ] dodać prosty heartbeat (`Ping`/`Pong`) co X sekund,
  - [ ] jeżeli przez N sekund nie ma żadnej wiadomości → uznać połączenie za zerwane.

## 2. Wielosesyjność i skalowalność

- [ ] Obecnie 1 proces = 1 gra (2 klientów).  
      Rozszerzenia:
  - [ ] zrobić warstwę „Lobby/Matchmaker”:
    - wiele `GwentServer` (albo wewnętrzna lista „pokoi”),
    - każdy pokój ma własną instancję `GameEngine` i listę 2 klientów,
    - TCP Listener jeden, ale mapujemy klientów do konkretnych gier.
  - [ ] rozważyć protokół z `MatchId` (w `NetworkMessage`), żeby mieć multi-match w jednym procesie.
- [ ] Możliwość **obserwatorów**:
  - [ ] trzeci (i więcej) klient tylko z prawem do `GameStateUpdate` (read-only),
  - [ ] brak możliwości wysyłania `GameAction`.

## 3. Konfiguracja i parametryzacja

- [ ] Plik konfiguracyjny serwera (JSON / appsettings):
  - [ ] domyślny port,
  - [ ] maksymalna liczba gier jednocześnie,
  - [ ] czas timeoutu,
  - [ ] ścieżki logów.
- [ ] Parametry inicjalizujące:
  - [ ] możliwość startu z argumentami CLI (`--port`, `--log-level`).

## 4. Protokół sieciowy

- [ ] **Wersjonowanie** protokołu:
  - [ ] dodać np. `ProtocolVersion` w `NetworkMessage`,
  - [ ] przy niezgodności wersji zwracać błąd „unsupported client version”.
- [ ] Uporządkować nazwy `MessageType`:
  - [ ] spisać w jednym enumie / static class (np. `MessageTypes.Hello`, `MessageTypes.GameAction`, `MessageTypes.GameStateUpdate`),
  - [ ] uniknąć „magic stringów” rozproszonych po kodzie.
- [ ] Formalna specyfikacja JSON:
  - [ ] przykładowe payloady:
    - `Hello` (nick, frakcja, czy host),
    - `SessionConfiguration`,
    - `GameAction`,
    - `GameStateUpdate`,
    - `Error`.

## 5. Integracja z Gwent.Core

- [ ] Upewnić się, że:
  - [ ] serwer **nigdy nie modyfikuje** `GameBoardState` ręcznie – wszystko przez `GameEngine`,
  - [ ] po każdej akcji **zawsze**:
    - `gameEngine.ApplyAction(payload)`,
    - `var state = gameEngine.GetBoardStateSnapshot();`,
    - wysłanie `GameStateUpdate` do obu klientów.
- [ ] Możliwe usprawnienia:
  - [ ] dodać „diffy” stanu (wysyłać tylko to, co się zmieniło), gdyby pełny `GameBoardState` był za duży (na razie niepotrzebne, ale do rozważenia).
- [ ] Dodać test „end-to-end”:
  - [ ] serwer uruchomiony in-proc,
  - [ ] dwóch sztucznych klientów (np. TcpClienty w testach),
  - [ ] przeprowadzić prostą rozgrywkę i sprawdzić, czy:
    - tury się zmieniają,
    - `GameBoardState` jest spójny na obu klientach,
    - gra kończy się poprawnie.

## 6. Logowanie i monitoring

- [ ] Wspólny interfejs logowania (np. `IServerLogger`):
  - [ ] domyślnie log do konsoli,
  - [ ] opcjonalnie log do pliku / serwisu (np. Serilog, NLog).
- [ ] Dodać logi przy:
  - [ ] podłączeniu/rozłączeniu klienta,
  - [ ] błędach deserializacji JSON,
  - [ ] każdej akcji GameAction (kto, co, kiedy),
  - [ ] zakończeniu meczu (zwycięzca, powód).
- [ ] Liczniki (metrics):
  - [ ] liczba rozegranych gier,
  - [ ] średni czas trwania meczu,
  - [ ] liczba rozłączeń w trakcie gry.

## 7. Bezpieczeństwo

- [ ] Walidacja wejścia:
  - [ ] upewnić się, że klient nie może:
    - zagrać karty, której nie ma w ręce,
    - wykonać akcji poza swoją turą,
    - wysłać akcji po `IsGameFinished == true` (po stronie serwera i tak ignorowane, ale warto logować).
- [ ] Ograniczenie floodu:
  - [ ] prosta ochrona przed spamowaniem wiadomościami (np. limit X GameAction na sekundę),
  - [ ] przy przekroczeniu – rozłączyć klienta.

## 8. Refaktoryzacja kodu

- [ ] Rozbić `GwentServer` na mniejsze klasy:
  - [ ] `ClientConnection` – stan jednego klienta (TcpClient, nickname, rola),
  - [ ] `MatchContext` – opakowanie `GameEngine` + lista klientów w tej grze,
  - [ ] główny listener tylko routuje połączenia do odpowiednich `MatchContext`.
- [ ] Wyciągnąć wspólne fragmenty:
  - [ ] obsługa JSON-line protocol,
  - [ ] serializacja/deserializacja `NetworkMessage`,
  - [ ] helper do broadcastu do wszystkich klientów meczu.

## 9. Dokumentacja

- [ ] Uzupełnić `Gwent.Server_Documentation.md`:
  - [ ] diagram sekwencji (Client → Server → GameEngine → Server → Client),
  - [ ] diagram klas serwera.
- [ ] Dodać przykładowy flow:
  - [ ] od uruchomienia serwera,
  - [ ] przez podłączenie hosta i guest,
  - [ ] do zakończenia meczu (z logiem komunikatów).
