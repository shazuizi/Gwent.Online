```markdown
# Gwent.Core — Dokumentacja techniczna

## 1. Cel biblioteki

`Gwent.Core` to autorytatywny silnik gry Gwint (Wiedźmin 3), w pełni niezależny od:
- UI,
- sieci,
- systemu operacyjnego,
- frameworka (WPF, Unity, Blazor, MAUI, Web…).

Silnik:

- przechowuje pełny **stan gry** (`GameBoardState`)
- przyjmuje **intencje gracza** w formie `GameActionPayload`
- waliduje akcje (czy są legalne)
- wykonuje efekty kart i zasady frakcji
- kończy rundy i całą grę
- zwraca zaktualizowany stan

---

## 2. Główne komponenty

### 2.1. GameBoardState.cs
Opisuje aktualny stan całej gry, zawiera:

- `HostPlayerBoard`, `GuestPlayerBoard`
- rzędy jednostek
- karty pogody
- liderów
- rundy, życia, tura
- zakończenie gry
- log gry (`GameLog`)
- bierną wiedzę o tym, co widzi UI

Żaden kod poza `GameEngine` **nie może** modyfikować tego obiektu.

---

### 2.2. PlayerBoardState
Stan pojedynczego gracza:

- `Deck`
- `Hand`
- `Graveyard`
- `MeleeRow`, `RangedRow`, `SiegeRow`
- `Faction`
- `LifeTokensRemaining`
- `RoundsWon`
- `HasPassedCurrentRound`
- `MulligansRemaining`
- `LeaderCard`, `LeaderAbilityUsed`

Metody pomocnicze:
- `GetTotalStrength()`
- `GetRowStrength(row)`
- `EnumerateBoardCards()`

---

### 2.3. GwentCard / CardDefinition
Pełny opis kart, zgodny z Gwint W3.

**CardDefinition**:
- stałe właściwości: nazwa, siła bazowa, abilities, grupa muster, rząd domyślny

**GwentCard (instance)**:
- `InstanceId`
- `Definition`
- `CurrentStrength`
- `IsOnBoard`

**Każde zagranie** używa `InstanceId` — to umożliwia targetowanie Medica, Decoya, Mardroeme itd.

---

### 2.4. GameActionPayload
Intencja gracza przekazywana z klienta:

ActionType : GameActionType
ActingPlayerNickname : string
CardInstanceId : Guid
TargetInstanceId : Guid? // opcjonalnie
TargetRow : CardRow?

### 2.5. GameEngine
Najważniejsza klasa — JEDYNE miejsce, gdzie:

- obowiązują zasady gry,
- sprawdzana jest poprawność akcji,
- wykonywane są efekty,
- liczone są rundy,
- liczone są buffy i debuffy,
- obsługiwany jest pass i tura,
- kończy się gra.

---

## 3. Cykl życia gry w Core

1. `GameEngine` tworzony z `GameSessionConfiguration`.
2. Losowanie / zasady Scoia’tael ustalają pierwszego gracza.
3. Każdy gracz dobiera rękę (10 kart).
4. Rundy 1–3:
   - Mulligan (2 wymiany)
   - Następują tury graczy:
     - PlayCard
     - PassTurn
     - UseLeaderAbility
     - Surrender
   - Jeśli obaj pass → `EndRound`
5. Po `LifeTokensRemaining == 0` u jednego gracza → koniec gry
6. Jeśli obaj stracą życie → remis (WinnerNickname = null)

---

## 4. Mechanizmy gry w Core

### 4.1. Walidacja (ValidateAction)
Jedno miejsce, które decyduje **czy w ogóle wolno wykonać akcję**:

- czy gra skończona
- czy akcja należy do aktywnego gracza
- pass lock
- mulligan lock
- leader ability lock
- czy karta jest w ręce
- czy target istnieje i jest dozwolony

Wynik walidacji:
- true → akcja wykonywana
- false → akcja ignorowana (klient jedynie dostaje zaktualizowany stan)

---

### 4.2. Efekty kart (ApplyPlayCardAction)
Wspiera **pełną logikę W3**, m.in.:

- **Spy**
- **Medic**
- **Decoy**
- **Mardroeme**
- **Agile**
- **Muster**
- **TightBond**
- **MoralBoost**
- **CommandersHorn**
- **Scorch (global)** i Scorch rządowy
- **Weather**: Frost, Fog, Rain
- **ClearWeather**

---

### 4.3. RecalculateStrengths
- reset sił kart
- zastosowanie pogody
- buffy:
  - tight bond
  - morale
  - horn
- wyjątki dla hero (nie są buffowane/debuffowane)

---

### 4.4. EndRound
Obsługuje:

- Northern Realms bonus (dobranie 1 karty)
- Nilfgaard bonus (wygrana remisu)
- Monsters bonus (jednostka pozostaje)
- Scoia’tael — wybór kto zaczyna (opcjonalne w payload)
- remis rundy
- remis gry

---

## 5. Kontrakty i rozszerzalność

### Dodanie nowej karty:
- dodaj `CardDefinition`
- jeśli wymaga specjalnej logiki → dopisz w `ApplyPlayCardAction` lub `RecalculateStrengths`

### Dodanie frakcji:
- dopisz w `FactionType`
- dopisz pasywki w `EndRound`

### Dodanie nowej akcji:
- dopisz w `GameActionType`
- obsłuż w `ApplyGameAction` + `ValidateAction`

---