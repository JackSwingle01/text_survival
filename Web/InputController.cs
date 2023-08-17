using Microsoft.AspNetCore.Mvc;

namespace text_survival_rpg_web.Web
{
    [ApiController]
    [Route("[controller]")]
    public class InputController : ControllerBase
    {

        public class InputData
        {
            public string Input { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] InputData data)
        {
            Input.OnUserInputReceived(data.Input);
            return Ok();
        }


    }
}
