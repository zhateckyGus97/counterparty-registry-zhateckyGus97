using CounterpartyRegistry.Api.Models;
using CounterpartyRegistry.Api.Validation;
using CounterpartyRegistry.Application.Abstractions;
using CounterpartyRegistry.Application.Common;
using CounterpartyRegistry.Application.Founders;
using Microsoft.AspNetCore.Mvc;

namespace CounterpartyRegistry.Api.Controllers;

[ApiController]
[Route("api/founders")]
[ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class FoundersController : ControllerBase
{
    private readonly IFounderService _founders;

    public FoundersController(IFounderService founders)
    {
        _founders = founders;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<FounderResponse>>> GetPage([FromQuery] PageQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await _founders.GetPageAsync(new PageRequest(query.Page, query.PageSize), cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FounderResponse>> GetById([NotEmptyGuid] Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await _founders.GetByIdAsync(id, cancellationToken));
    }

    [HttpPost]
    [ProducesResponseType(typeof(FounderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FounderResponse>> Create(FounderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _founders.CreateAsync(new CreateFounderRequest(request.Inn!, request.FullName!), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FounderResponse>> Update([NotEmptyGuid] Guid id, FounderRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _founders.UpdateAsync(id,
            new UpdateFounderRequest(request.Inn!, request.FullName!), cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete([NotEmptyGuid] Guid id, CancellationToken cancellationToken)
    {
        await _founders.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
