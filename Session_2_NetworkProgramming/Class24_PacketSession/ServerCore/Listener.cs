using System;
using System.Net;
using System.Net.Sockets;

namespace ServerCore
{
    public class Listener
    {
        private Socket _listenSocket;
        private Func<Session> _sessionFactory;

        public void Init(IPEndPoint endPoint, Func<Session> sessionFactory, int backlog = 10)
        {
            _sessionFactory = sessionFactory;
            
            _listenSocket = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp
            );
            
            _listenSocket.Bind(endPoint);
            _listenSocket.Listen(backlog);
            
            Console.WriteLine($"[Listener] 시작: {endPoint}");
        }

        public void StartAccept()
        {
            SocketAsyncEventArgs args = new SocketAsyncEventArgs();
            args.Completed += OnAcceptCompleted;
            RegisterAccept(args);
        }

        private void RegisterAccept(SocketAsyncEventArgs args)
        {
            args.AcceptSocket = null;
            
            bool pending = _listenSocket.AcceptAsync(args);
            if (!pending)
            {
                OnAcceptCompleted(null, args);
            }
        }

        private void OnAcceptCompleted(object sender, SocketAsyncEventArgs args)
        {
            if (args.SocketError == SocketError.Success)
            {
                Session session = _sessionFactory.Invoke();
                session.Start(args.AcceptSocket);
            }
            else
            {
                Console.WriteLine($"[Listener] Accept 실패: {args.SocketError}");
            }
            
            RegisterAccept(args);
        }

        public void Stop()
        {
            _listenSocket.Close();
        }
    }
}