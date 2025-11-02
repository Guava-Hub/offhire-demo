using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using OffHire.Api.Contracts;
using OffHire.Application.Commands;
using OffHire.Domain.Models;

namespace OffHire.Api.Controllers;

[ApiController]
[Route("api/offhire")]
public sealed class OffHireController : ControllerBase
{
    private readonly UpsertOffHireOrderCommandHandler _commandHandler;

    public OffHireController(UpsertOffHireOrderCommandHandler commandHandler)
    {
        _commandHandler = commandHandler;
    }

    [HttpPost("update")]
    public async Task<IActionResult> UpdateOffHire([FromBody] UpdateOffHireRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var contractNumber = request.OffHireOrders.Select(o => o.Header.SpeedyReferences.ContractNumber).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(contractNumber))
        {
            return BadRequest("Contract number is required.");
        }

        var firstOrder = request.OffHireOrders.First();
        var command = new UpsertOffHireOrderCommand(
            ContractNumber: contractNumber,
            RentalNumber: contractNumber,
            AccountNumber: string.Empty,
            CompanyCode: "sas",
            Originator: new Originator("WebAPI", "ExternalClients", string.Empty),
            RequestedAt: request.DateTimeRequested,
            Lines: firstOrder.Lines.Select(line => new LineRequest(
                line.ExternalLineNumberRef,
                line.LineNumberReference,
                line.Quantity,
                line.OffHireDateTime,
                line.CollectionInstructions)).ToList());

        var aggregate = await _commandHandler.Handle(command, cancellationToken);
        return Ok(new { aggregate.Id, Lines = aggregate.Lines.Select(line => new { line.ExternalLineNumberRef, RequestedQuantity = line.RequestedQuantity.Value }) });
    }
}
