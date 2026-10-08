namespace Mezon.Net.Sdk.Interactions
{
    /// <summary>
    /// Describes how the actor id on an interaction was established.
    /// </summary>
    public enum InteractionActorTrust
    {
        Unknown = 0,

        /// <summary>
        /// The server forwarded the actor id exactly as the sending client supplied it, so any logged-in user can
        /// claim to be someone else. Button clicks currently arrive this way.
        /// </summary>
        ClientSupplied = 1,

        /// <summary>
        /// The server replaced the actor id with the authenticated session user. Dropdown selections arrive this way.
        /// </summary>
        ServerAuthenticated = 2,
    }
}
