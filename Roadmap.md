```markdown
# Gwent — Roadmap

## ETAP 1 — Stabilna wersja PvP (obecna faza)
### Core
- [x] pełny GameEngine
- [x] pełna walidacja akcji
- [x] wszystkie efekty kart
- [x] obsługa rund + frakcji

### Server
- [x] anti-spoofing (TcpClient → PlayerIdentity)
- [x] broadcast stanu gry
- [x] rozłączenie gracza kończy grę

### Client
- [x] UI planszy
- [x] mulligan UI
- [x] play/pass/leader ability
- [x] targetowanie (medic/decoy/mardroeme)

---

## ETAP 2 — Uporządkowanie i czystość architektury
### Core:
- [ ] rozbić GameEngine na:
  - StrengthCalculator
  - RoundManager
  - CardEffectService
  - ActionValidator
- [ ] testy jednostkowe (xUnit / NUnit)

### Server:
- [ ] wyprowadzić protokół do `Gwent.Shared`
- [ ] dodać heartbeat / pingi
- [ ] obsługa reconnect

### Client:
- [ ] przepisanie GamePage na MVVM
- [ ] nowy UI kart (tooltips, grafiki, ikony)

---

## ETAP 3 — Tryby gry
- [ ] PvE — bot z heurystyką
- [ ] Tryb 1-rundowy (Arena)
- [ ] Spectator mode

---

## ETAP 4 — Deckbuilder
- [ ] edytor talii jak w W3
- [ ] zapis do JSON
- [ ] walidacja talii
- [ ] integracja z Core (DeckFactory czyta JSON)

---

## ETAP 5 — Online matchmaking
- [ ] Lobby / kolejka
- [ ] Matchmaking rating (ELO/Glicko)
- [ ] Serwer dedykowany osobno od silnika

---

## ETAP 6 — Release
- [ ] installer
- [ ] automatyczne update
- [ ] logowanie błędów + telemetria (opcjonalnie)
- [ ] oficjalne API i dokumentacja

---