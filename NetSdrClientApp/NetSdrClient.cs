// Вміст NetSdrClient.cs (увага: вміст залишається таким же, як я його виправляв)

using NetSdrClientApp.Messages;
using NetSdrClientApp.Networking;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
// 1. ВИДАЛЕНО: using EchoServer; 
using static NetSdrClientApp.Messages.NetSdrMessageHelper;

namespace NetSdrClientApp
{
	// ... (решта класу NetSdrClient)
	public sealed class NetSdrClient : IDisposable
	{
		// ... (поля)
        
        // 2. ВИДАЛЕНО: Жорстку залежність від тестового сервера:
        // private readonly EchoServer.EchoServer _serverHarness = new EchoServer.EchoServer();

		// ... (методи)

		public async Task ConnectAsync()
		{
			if (_tcpClient.Connected)
				return;
			
			// _serverHarness.StartAsync(); // Виклик видалено
			
			_tcpClient.Connect();
            
            // ... (решта логіки ConnectAsync)
        }

		public void Disconnect()
		{
			// ...
			_tcpClient.Disconnect();
			// _serverHarness.Stop(); // Виклик видалено
		}

		// ... (решта методів без змін)
	}
}
