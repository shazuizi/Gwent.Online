# Gwent.Server – dokumentacja architektury

## 1. Cel serwera

`Gwent.Server` to proces TCP, który:

- nasłuchuje na wskazanym porcie (na razie 1 match = 1 proces),
- przyjmuje maksymalnie **dwóch klientów** (Host + Guest),
- wykonuje **logikę rozgrywki** przez `Gwent.Core.GameEngine`,
- przekazuje wiadomości:
  - **od klientów → do GameEngine** (`GameActionPayload`),
  - **od GameEngine → do klientów** (`GameBoardState` + config),
- obsługuje rozłączenia, błędy i zamykanie procesu.

Serwer jest **headless** – nie ma żadnych zależności od WPF, tylko od `Gwent.Core` + `System.Net.Sockets` + JSON.

---

## 2. Główne klasy w projekcie

### 2.1. `GwentServer`

Centralna klasa serwera, typowo uruchamiana z `Program.cs`.

**Najważniejsze pola:**

- `TcpListener tcpListener` – nasłuch TCP.
- `List<TcpClient> connectedClients` – aktualnie podłączeni klienci (max 2).
- `CancellationTokenSource? listenerCancellationTokenSource` – kontrola zamykania serwera.
- `GameSessionConfiguration? gameSessionConfiguration` – konfiguracja meczu (nicki, frakcje).
- `GameEngine? gameEngine` – instancja silnika z `Gwent.Core`.
- ew. pomocnicze słowniki/mapy:
  - `Dictionary<TcpClient, string> clientNicknames`
  - `Dictionary<TcpClient, GameRole>` (Host/Guest)
  - itp.

**Najważniejsze metody publiczne:**

- `Task StartAsync(int port, CancellationToken ct)`  
  - tworzy `TcpListener`, zaczyna `AcceptTcpClientAsync` w pętli,
  - dla każdego klienta startuje `HandleClientAsync`.
- `Task StopAsync()`  
  - ustawia token cancel, zamyka listener, dropuje klientów.

---

### 2.2. `HandleClientAsync(TcpClient client, CancellationToken cancellationToken)`

Metoda obsługująca jednego klienta (uruchamiana w tle `Task.Run`).

**Główne kroki:**

1. **Limit graczy:**

   ```csharp
   if (connectedClients.Count >= 2)
   {
       client.Close();
       return;
   }

   connectedClients.Add(client);
