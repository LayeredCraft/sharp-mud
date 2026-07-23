namespace SharpMud.Engine.Commands;

/// <summary>
/// The role hierarchy (<see cref="SecurityRole.FullAdmin"/> implies <see
/// cref="SecurityRole.MinorAdmin"/> implies <see cref="SecurityRole.Player"/>;
/// <see cref="SecurityRole.FullBuilder"/> implies <see
/// cref="SecurityRole.MinorBuilder"/> implies <see cref="SecurityRole.Player"/>
/// - the admin and builder ladders are deliberately independent of each
/// other, both bottoming out at <see cref="SecurityRole.Player"/>), defined
/// once here and used by both <c>PlayerBehavior.GrantRole</c> (accumulate
/// downward) and <c>PlayerBehavior.RevokeRole</c> (check upward) per
/// ADR-0005's accumulation rule.
/// </summary>
public static class SecurityRoleExtensions
{
    extension(SecurityRole role)
    {
        /// <summary>
        /// <paramref name="role"/> itself, plus every role it implies.
        /// Assumes <paramref name="role"/> is a single flag - this repo's
        /// only callers (<c>GrantRole</c>, admin commands validated
        /// against an individually-grantable allowlist) always pass one.
        /// </summary>
        public SecurityRole ImpliedRoles => role switch
        {
            SecurityRole.FullAdmin => SecurityRole.FullAdmin | SecurityRole.MinorAdmin | SecurityRole.Player,
            SecurityRole.MinorAdmin => SecurityRole.MinorAdmin | SecurityRole.Player,
            SecurityRole.FullBuilder => SecurityRole.FullBuilder | SecurityRole.MinorBuilder | SecurityRole.Player,
            SecurityRole.MinorBuilder => SecurityRole.MinorBuilder | SecurityRole.Player,
            _ => role,
        };

        /// <summary>Whether holding <paramref name="role"/> implies <paramref name="other"/>.</summary>
        public bool Implies(SecurityRole other) => (role.ImpliedRoles & other) == other;
    }
}
