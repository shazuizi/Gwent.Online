# Gwent.Core – dokumentacja architektury

## 1. Cel biblioteki

`Gwent.Core` to samodzielny silnik logiki gry Gwint (WiedŸmin 3):

- **bez zale¿noœci od UI, sieci, WPF, itp.**
- przyjmuje **GameActionPayload** (akcje gracza) i modyfikuje **GameBoardState**,
- odzwierciedla zasady W3 na poziomie:
  - frakcji (`NorthernRealms`, `Nilfgaard`, `Scoiatael`, `Monsters`),
  - rund, ¿yæ, remisów,
  - mulliganu,
  - umiejêtnoœci kart (Spy, Medic, Muster, TightBond, MoralBoost, Horn, Scorch, pogoda, Decoy, Mardroeme),
  - pasywnych bonusów frakcji.

Docelowo UI / serwer tylko serializuje/deserialize `GameActionPayload` oraz `GameBoardState`.

---

## 2. Przegl¹d najwa¿niejszych typów

### 2.1. Enums (`Enums.cs`)

- `FactionType` – frakcja talii:
  - `Neutral`, `NorthernRealms`, `Nilfgaard`, `Scoiatael`, `Monsters`
- `CardRow` – rz¹d na planszy:
  - `Melee`, `Ranged`, `Siege`, `Agile`, `WeatherGlobal`
- `CardCategory` – typ karty:
  - `Unit`, `Hero`, `Weather`, `Special`, `Leader`
- `CardAbilityType` – cechy / zdolnoœci:
  - jednostki: `Spy`, `Medic`, `TightBond`, `Muster`, `Agile`, `MoralBoost`, `CommandersHorn`, `Scorch`, `Hero`
  - pogoda: `WeatherBitingFrost`, `WeatherImpenetrableFog`, `WeatherTorrentialRain`, `ClearWeather`
  - specjalne: `Decoy`, `Mardroeme`
  - dowódcy: np. `LeaderAbility_DrawExtraCard`
- `GameActionType` – typ akcji z klienta:
  - `PlayCard`, `Mulligan`, `PassTurn`, `UseLeaderAbility`, `Resign`

---

### 2.2. Definicje kart i egzemplarze

#### `CardDefinition`

Statyczny opis karty (jak w kolekcji):

- `Id` – unikalny string (np. `"nr_blue_stripes_commando"`),
- `Name`
- `Faction` – do jakiej frakcji nale¿y,
- `Category` – `Unit`, `Hero`, `Weather`, `Special`, `Leader`,
- `DefaultRow` – domyœlny rz¹d (dla jednostek),
- `BaseStrength` – bazowa si³a,
- `Abilities` – lista `CardAbilityType`,
- `MusterGroup` – identyfikator grupy zestawów Muster,
- `TightBondGroup` – id grupy TightBond,
- `IsHero` – wynikaj¹cy z kategorii/ability.

Definicje kart s¹ **wspó³dzielone** przez wszystkie egzemplarze.

#### `GwentCard`

Konkretny egzemplarz w grze:

- `InstanceId : Guid` – unikalny identyfikator egzemplarza (do targetowania z klienta),
- `Definition : CardDefinition` – referencja do definicji,
- skróty: `Name`, `Faction`, `Category`, `DefaultRow`, `Abilities`, `MusterGroup`, `TightBondGroup`, `IsHero`,
- `CurrentStrength` – aktualna si³a po efektach,
- `IsOnBoard` – czy karta le¿y na stole.

Silnik **nie modyfikuje** definicji, tylko pola egzemplarza (`CurrentStrength`).

---

### 2.3. Stan gracza i stan gry

#### `PlayerBoardState`

Stan jednego gracza:

- `PlayerNickname`
- `Faction : FactionType`
- Strefy:
  - `Deck : List<GwentCard>`
  - `Hand : List<GwentCard>`
  - `Graveyard : List<GwentCard>`
  - rzêdy:
    - `MeleeRow`, `RangedRow`, `SiegeRow`
- Lider:
  - `LeaderCard : GwentCard?`
  - `LeaderAbilityUsed : bool`
- Rundy / ¿ycia:
  - `LifeTokensRemaining` (domyœlnie 2)
  - `RoundsWon`
  - `HasPassedCurrentRound`
  - `MulligansRemaining` (domyœlnie 2)
- Metody pomocnicze:
  - `EnumerateBoardCards()` – wszystkie karty na stole,
  - `GetRowStrength(CardRow)` – si³a rzêdu,
  - `GetTotalStrength()` – suma wszystkich rzêdów.

#### `GameBoardState`

Pe³ny stan gry:

- `HostPlayerBoard : PlayerBoardState`
- `GuestPlayerBoard : PlayerBoardState`
- `WeatherCards : List<GwentCard>` – zagrane karty pogody
- `CurrentRoundNumber`
- `ActivePlayerNickname` – nick gracza, który ma turê
- `IsGameFinished`
- `WinnerNickname` – `null` oznacza remis meczu
- `GameLog : List<string>` – prosty log tekstowy (do UI/debugu)

---

### 2.4. Konfiguracja sesji

#### `PlayerIdentity`

- `Nickname`
- `IsHost`
- `Faction : FactionType`

#### `GameSessionConfiguration`

- `HostPlayer : PlayerIdentity`
- `GuestPlayer : PlayerIdentity`

Konfiguracja jest przekazywana do `GameEngine` przy starcie – na jej podstawie tworzone s¹ `PlayerBoardState` i decki.

---

### 2.5. Akcje z klienta

#### `GameActionPayload`

- `ActionType : GameActionType`
- `ActingPlayerNickname : string` – kto wykonuje akcjê,
- `CardInstanceId : Guid`
- `TargetInstanceId : Guid?` – opcjonalny cel (Medic/Decoy/Mardroeme),
- `TargetRow : CardRow?` – rz¹d docelowy (jednostki / rogi / specjalne nie-globalne).

UI / klient sieciowy tworzy `GameActionPayload`, serializuje i wysy³a go do serwera, a ten przekazuje do `GameEngine.ApplyAction`.

---

## 3. DeckFactory

`DeckFactory` jest odpowiedzialne za budowê talii startowych:

- `CreateStarterDeckDefinitions(FactionType)` – zwraca listê `CardDefinition` dla danej frakcji (w tej chwili zawiera tylko szkielet + przyk³ad).
- `CreateDeckInstances(FactionType)` – zamienia definicje na egzemplarze `GwentCard`.

Docelowo tutaj mo¿na:

- trzymaæ **predefiniowane talie W3**,  
- lub ³adowaæ talie budowane przez gracza (np. z JSON/DB).

---

## 4. GameEngine – cykl gry

### 4.1. Tworzenie silnika

```csharp
var sessionConfig = new GameSessionConfiguration
{
    HostPlayer = new PlayerIdentity { Nickname = "Host", Faction = FactionType.NorthernRealms },
    GuestPlayer = new PlayerIdentity { Nickname = "Guest", Faction = FactionType.Nilfgaard }
};

var engine = new GameEngine(sessionConfig);
GameBoardState state = engine.GetBoardStateSnapshot();
