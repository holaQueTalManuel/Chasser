using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chasser.Common.Network;
using Chasser.Logic.Network;

namespace Chasser.Logic
{
    public static class AuthHelper
    {
        private static string _authToken;
        private readonly static string path = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                @"..\..\..\..\Chasser\Logic\Network"
            );
        public static string GetToken() => _authToken;

        public static void SetToken(string token)
        {
            _authToken = token;
            // Opcional: Guardar en archivo/configuración para persistencia
            //File.WriteAllText(path, token);
        }

        //public static async Task<bool> IsUserAuthenticatedAsync()
        //{
        //    if (string.IsNullOrEmpty(_authToken))
        //    {
        //        // Intentar cargar de archivo si existe
        //        if (File.Exists(path))
        //        {
        //            _authToken = File.ReadAllText(path);
        //        }
        //        else
        //        {
        //            return false;
        //        }
        //    }

        //    try
        //    {
        //        var request = new RequestMessage
        //        {
        //            Command = "VALIDATE_TOKEN",
        //            Data = new Dictionary<string, string> { { "token", _authToken } }
        //        };

        //        var response = await TCPClientManager.SendMessageAsync(request);
        //        return response.Status == "TOKEN_VALID";
        //    }
        //    catch
        //    {
        //        return false;
        //    }
        //}

        public static void ClearAuth()
        {
            _authToken = null;
            if (File.Exists("user_token.txt"))
                File.Delete("user_token.txt");
        }
    }
}
