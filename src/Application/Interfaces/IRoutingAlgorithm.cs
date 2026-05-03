using src.Domain.Entities;

namespace src.Application.Interfaces;

public interface IRoutingAlgorithm
{
    string Name { get; }
    RoutingResult Route(Channel channel);
}
