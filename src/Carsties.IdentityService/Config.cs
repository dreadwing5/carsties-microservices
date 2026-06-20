using Duende.IdentityServer.Models;

namespace Carsties.IdentityService;

public static class Config
{
    public static IEnumerable<IdentityResource> IdentityResources =>
        [new IdentityResources.OpenId(), new IdentityResources.Profile()];

    public static IEnumerable<ApiScope> ApiScopes =>
        [new ApiScope("auctionApp", "Auction app full access")];

    public static IEnumerable<Client> Clients =>
        [
            // For Development Purpose - NEVER use this in production!!!
            new Client
            {
                ClientId = "postman",
                ClientName = "Postman",
                AllowedScopes = ["openid", "profile", "auctionApp"],
                RedirectUris = ["https://www.getpostman.com/oauth2/callback"],
                AllowedGrantTypes = { GrantType.ResourceOwnerPassword },
                ClientSecrets = [new Secret("NotASecret".Sha256())],
            },
        ];
}
