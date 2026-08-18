using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace SugarShop.Web.SessionModels
{
    public static class BoxSessionExtensions
    {
        public static void SetBoxSessionState(this ISession session, BoxSessionState state)
        {
            session.SetString(BoxSessionState.SessionKey, JsonSerializer.Serialize(state));
        }

        public static BoxSessionState GetBoxSessionState(this ISession session)
        {
            var json = session.GetString(BoxSessionState.SessionKey);
            if (string.IsNullOrWhiteSpace(json))
                return new BoxSessionState();

            return JsonSerializer.Deserialize<BoxSessionState>(json) ?? new BoxSessionState();
        }
    }
}