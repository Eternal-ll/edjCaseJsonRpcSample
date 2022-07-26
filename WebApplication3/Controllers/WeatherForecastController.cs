using EdjCase.JsonRpc.Router;
using EdjCase.JsonRpc.Router.Defaults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace WebApplication3.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [RpcRoute("api/jsonrpc/[controller]")]
    [Produces("application/json")]
    // Uncomment this and it will expose all public methods for open API and it will not ignore
    // RpcController.Ok and RpcController.Error
    //[ApiExplorerSettings(GroupName = "json")]
    public class WeatherForecastController : RpcController
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<WeatherForecastController> _logger;

        public WeatherForecastController(ILogger<WeatherForecastController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// GetWeatherForecast
        /// </summary>
        /// <returns>Returns something</returns>
        [HttpGet(Name = "GetWeatherForecast")]
        public IEnumerable<WeatherForecast> Get()
        {
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
        }
        /// <summary>
        /// Desc....
        /// </summary>
        /// <param name="login">Login</param>
        /// <param name="password">Password</param>
        /// <param name="crc">Sign</param>
        /// <param name="time">Time</param>
        /// <response code="201">Returns the newly created item</response>
        /// <response code="400">If the item is null</response>
        /// <returns></returns>
        [HttpPost("[action]")]
        [ApiExplorerSettings(GroupName = "json")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Service[]))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(RpcMethodErrorResult))]
        public async Task<object> PutThere([BindRequired] string login, [BindRequired] string password, [BindRequired] string crc, [BindRequired] long time)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// Desc....
        /// </summary>
        /// <param name="login">Login</param>
        /// <param name="password">Password</param>
        /// <param name="crc">Sign</param>
        /// <param name="time">Time</param>
        /// <response code="201">Returns the newly created item</response>
        /// <response code="400">If the item is null</response>
        /// <returns></returns>
        [RpcIgnore]
        [HttpPost("[action]")]
        [ApiExplorerSettings(GroupName = "json")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Service[]))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(RpcMethodErrorResult))]
        public async Task<object> PutSomething([BindRequired] string login, [BindRequired] string password, [BindRequired] string crc, [BindRequired] long time)
        {
            throw new NotImplementedException();
        }

    }
    public class Service
    {
        public string Name { get; set; }
        public string Description { get; set; }
    }
}