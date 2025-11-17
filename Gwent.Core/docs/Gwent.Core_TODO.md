
```markdown
# Gwent.Core – TODO / roadmap

## 1. Zasady 1:1 z W3

- [ ] Uzupełnić **pełne talie** dla wszystkich frakcji w `DeckFactory.CreateStarterDeckDefinitions`:
  - [ ] Northern Realms
  - [ ] Nilfgaard
  - [ ] Scoia'tael
  - [ ] Monsters
- [ ] Zweryfikować liczby kopii kart + dokładne wartości siły pod W3.
- [ ] Doprecyzować działanie **Mardroeme**:
  - [ ] Rozróżnić buff/debuff w zależności od typu celu (jak w W3).
- [ ] Dodać brakujące specjalne efekty, jeśli jakieś wyjątkowe karty tego wymagają:
  - [ ] Scorch rządowy / specyficzny,
  - [ ] specyficzne lider abilities (np. przesunięcie karty, podwójne użycie Scorch, itp.).
- [ ] Rozszerzyć `LeaderAbility`:
  - [ ] Dodać wszystkie lider abilities z W3 jako osobne wartości w `CardAbilityType`.
  - [ ] Zaimplementować je w `ApplyLeaderAbilityAction`.
- [ ] Scoia'tael – wybór, kto zaczyna:
  - [ ] Zamiast automatycznie startować Scoia'tael, dać mechanizm:
        gracz Scoia'tael *decyduje* (klient wysyła specjalną akcję `ChooseStartingPlayer`).

## 2. Decki / personalizacja

- [ ] Oddzielić `DeckFactory` od samego silnika jako osobny moduł („kolekcja kart”).
- [ ] Dodać mechanizm ładowania decków z zewnętrznego formatu:
  - [ ] JSON (lista `CardDefinition.Id` + `count`),
  - [ ] lub prosty własny format tekstowy.
- [ ] Dodać walidację talii:
  - [ ] maksymalna liczba kopii,
  - [ ] minimalna/maksymalna liczba kart.
- [ ] Przygotować testowe talie „balanced” dla PvP.

## 3. Integracja sieciowa

*(większość po stronie serwera/klienta, ale warto zapisać)*

- [ ] Zdefiniować wspólny kontrakt serializacji:
  - [ ] `GameActionPayload` i `GameBoardState` jako JSON (dokładne nazwy pól),
  - [ ] wersjonowanie protokołu (np. pole `ProtocolVersion`).
- [ ] Zbudować prosty **adapter** dla serwera:
  - [ ] klasę `GwentMatch` opakowującą `GameEngine` i komunikację z dwoma klientami.
- [ ] Ujednolicić logikę reconnect (np. przy utracie połączenia klienta).

## 4. API silnika / czystość kodu

- [ ] Dodać interfejs `IGameEngine` (przydatne do mockowania / testów jednostkowych).
- [ ] Rozważyć rozbicie `GameEngine` na mniejsze klasy:
  - [ ] `RoundManager` – za rundy, życia, bonusy frakcji,
  - [ ] `CardEffectResolver` – za efekty kart (Scorch, Muster, Medic, itp.),
  - [ ] `StrengthCalculator` – za `RecalculateStrengths` i row effects.
- [ ] Zastąpić część `if/else` w `ApplyPlayCardAction` wzorcem **Command/Strategy**:
  - [ ] mapowanie `CardAbilityType` → handler (np. `ICardEffectHandler`).

## 5. Testy

- [ ] Dodać projekt `Gwent.Core.Tests`.
- [ ] Testy jednostkowe dla:
  - [ ] Mulliganu (limit, brak w kolejnych rundach),
  - [ ] Spy (karta po stronie przeciwnika, dociąg 2 kart),
  - [ ] Medic (ożywienie tylko jednostek nie-hero),
  - [ ] Muster (dobiera wszystkie kopie z decku i ręki),
  - [ ] TightBond (mnożnik siły zgodnie z liczbą kopii),
  - [ ] MoralBoost (nie buffuje samego źródła),
  - [ ] CommandersHorn (mnożenie siły, brak efektu na hero),
  - [ ] Pogoda (Frost/Fog/Rain) – siła max 1,
  - [ ] Scorch (usuwa wszystkie najsilniejsze jednostki, nie hero),
  - [ ] Northern Realms bonus (dociąg po wygranej rundzie),
  - [ ] Monsters bonus (jedna jednostka zostaje na planszy),
  - [ ] Nilfgaard bonus (wygrana remisu),
  - [ ] Remis rundy → obaj tracą życie,
  - [ ] Remis meczu (obaj tracą ostatnie życie).
- [ ] Testy integracyjne:
  - [ ] pełny przebieg meczu (2–3 rundy, różne frakcje, różne scenariusze).

## 6. Logging / debug

- [ ] Dodać poziomy logów (Info / Warning / Error) zamiast czystego stringa.
- [ ] Opcjonalnie: interfejs `IGameLogger` wstrzykiwany do `GameEngine`:
  - [ ] domyślna implementacja → loguje tylko do `BoardState.GameLog`,
  - [ ] alternatywna → loguje do osobnej konsoli, pliku, itp.

## 7. Wydajność / optymalizacja

- [ ] Przy większych deckach rozważyć:
  - [ ] strukturę indeksującą `MusterGroup` / `TightBondGroup`,
  - [ ] cache dla `RecalculateStrengths` (liczenie diffa zamiast całości).
- [ ] Profilowanie typowych scenariuszy (dużo efektów, dużo kart na stole).

## 8. Dokumentacja

- [ ] Rozszerzyć `Gwent.Core_Documentation.md` o:
  - [ ] diagram klas,
  - [ ] diagram przebiegu rundy (sequence diagram),
  - [ ] przykładowe JSON-y (`GameBoardState`, `GameActionPayload`).
- [ ] Dodać krótką „dev guide” dla UI:
  - [ ] jak mapować akcje z WPF/Unity/Blazora do `GameActionPayload`.
