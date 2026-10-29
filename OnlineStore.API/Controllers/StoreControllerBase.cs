using Microsoft.AspNetCore.Mvc;

namespace OnlineStore.API.Controllers;

[ApiController]
[ProducesResponseType<ProblemDetails>(400)]
[ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409)]
[ProducesResponseType<ProblemDetails>(500)]
public abstract class StoreControllerBase : ControllerBase;
