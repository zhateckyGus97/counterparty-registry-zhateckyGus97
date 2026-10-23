using CounterpartyRegistry.Api.Models;
using CounterpartyRegistry.Api.Validation;
using CounterpartyRegistry.Application.Abstractions;
using CounterpartyRegistry.Application.Clients;
using CounterpartyRegistry.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace CounterpartyRegistry.Api.Controllers;

[ApiController]
[Route("api/clients")]
[ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class ClientsController : ControllerBase
{
    private readonly IClientService _clients;

    public ClientsController(IClientService clients)
    {
        _clients = clients;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ClientSummaryResponse>>> GetPage(
        [FromQuery] ClientListQuery query, CancellationToken cancellationToken)
    {
        var result = await _clients.GetPageAsync(new PageRequest(query.Page, query.PageSize),
            query.Type, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientResponse>> GetById([NotEmptyGuid] Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await _clients.GetByIdAsync(id, cancellationToken));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ClientResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClientResponse>> Create(ClientRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _clients.CreateAsync(new CreateClientRequest(request.Inn!, request.Name!, request.Type!.Value),
            cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClientResponse>> Update([NotEmptyGuid] Guid id, ClientRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _clients.UpdateAsync(id,
            new UpdateClientRequest(request.Inn!, request.Name!, request.Type!.Value), cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete([NotEmptyGuid] Guid id, CancellationToken cancellationToken)
    {
        await _clients.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/founders/{founderId:guid}")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClientResponse>> AddFounder([NotEmptyGuid] Guid id,
        [NotEmptyGuid] Guid founderId, CancellationToken cancellationToken)
    {
        return Ok(await _clients.AddFounderAsync(id, founderId, cancellationToken));
    }

    [HttpDelete("{id:guid}/founders/{founderId:guid}")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClientResponse>> RemoveFounder([NotEmptyGuid] Guid id,
        [NotEmptyGuid] Guid founderId, CancellationToken cancellationToken)
    {
        return Ok(await _clients.RemoveFounderAsync(id, founderId, cancellationToken));
    }
}
