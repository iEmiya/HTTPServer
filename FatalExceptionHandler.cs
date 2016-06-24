using System;

namespace HTTPServer
{
	internal class FatalExceptionHandler
	{
		public static void Handle(Exception huh)
		{
			Console.WriteLine("UnhandledException caught : " + huh.Message);
			Server.Log.FatalException("Failed when running application", huh);
		}
	}
}