using RagAuth.Api.Contracts.Business;

namespace RagAuth.Api.Services.Business;

public static class BusinessMapper
{
    public static BusinessResponse ToResponse(Domain.Entities.Business business) =>
        new(
            business.Name,
            business.Description,
            business.Email,
            business.Telephone,
            business.Address,
            business.Details,
            business.CreatedAt,
            business.UpdatedAt);
}
