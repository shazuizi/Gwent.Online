# Gwent.Server — Dokumentacja

## 1. Cel serwera

`Gwent.Server` to **autorytatywna instancja gry**:

- utrzymuje JEDNĄ sesję `GameEngine` na pojedynek,
- rozdziela odpowiedzialności:
  - przyjmuje połączenia graczy (TCP),
  - mapuje: TcpClient → PlayerIdentity,
  - odbiera `NetworkMessage (GameActionPayload)`,
  - waliduje nadawcę (anti-spoofing),
  - przekazuje akcję do `GameEngine`,
  - wysyła do obu graczy aktualny `GameBoardState`.

Serwer nie wie nic o UI — tylko o logice i stanie.

---

## 2. Komponenty serwera

### 2.1. GwentServer.cs
Zawiera:

- listener TCP
- `connectedClients : List<TcpClient>`
- `connectedPlayers : Dictionary<TcpClient, PlayerIdentity>`
- obsługę protokołu:
  - linia = jeden `NetworkMessage` (JSON)
- obsługę join
- start gry
- pętlę odbioru akcji
- broadcast stanu gry

---

### 2.2. Obsługa join

Flow:

1. Klient wysyła `PlayerJoinRequestPayload`.
2. Serwer:
   - przypisuje Host / Guest,
   - zapisuje PlayerIdentity do `connectedPlayers`,
   - wysyła `PlayerJoinAcceptedPayload`.
3. Gdy **dwaj gracze** są połączeni:
   - serwer tworzy `GameEngine`,
   - wysyła `GameSessionConfiguration` do obu,
   - wysyła `BoardStateUpdate`.

---

### 2.3. Obsługa GameAction

Serwer:

1. Deserializuje `GameActionPayload`.
2. Sprawdza, czy TcpClient jest znanym graczem.
3. Sprawdza, czy payload.ActingPlayerNickname == PlayerIdentity.Nickname
   - jeśli nie → odrzuca spoofa.
4. Przekazuje akcję do `GameEngine`.
5. Wysyła nowy `GameBoardState`.

**Klient nie ma żadnej władzy nad logiką gry.**  
Może tylko poprosić o akcję — a silnik decyduje.

---

### 2.4. Połączenia i rozłączenia

- na rozłączenie jednego z graczy:
  - gra kończy się automatycznie,
  - drugi gracz dostaje komunikat końca gry,
  - serwer zwalnia sesję.

---

## 3. NetworkMessage i protokół

**MessageType**:
- `PlayerJoinRequest`
- `PlayerJoinAccepted`
- `GameStart`
- `GameAction`
- `BoardStateUpdate`
- `Error`
- `Disconnect`

Każda wiadomość = JSON + `\n`.

**PayloadJson** to model (np. `GameActionPayload`).

---

## 4. Skalowalność

Architektura pozwala:

- uruchomić wiele równoległych sesji (wystarczy GwentMatchManager),
- dodać matchmaking,
- dodać boty,
- dodać REST API.

---