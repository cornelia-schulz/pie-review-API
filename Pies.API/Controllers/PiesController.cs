﻿using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Pies.API.Models;
using Pies.API.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Pies.API.Controllers
{
    [ApiController]
    [Route("api/v1/pies")]
    public class PiesController : ControllerBase
    {
        private readonly IPiesRepository _piesRepository;
        private readonly IMapper _mapper;

        public PiesController(IPiesRepository piesRepository,
            IMapper mapper)
        {
            _piesRepository = piesRepository ?? throw new ArgumentNullException(nameof(piesRepository));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        [HttpGet]
        [HttpHead]
        // get pies and if query string is passed in then filter by query string
        public ActionResult<IEnumerable<PieDto>> GetPies([FromQuery] string name, string searchQuery)
        {
            var piesFromRepo = _piesRepository.GetPies(name, searchQuery);
            var pies = new List<PieDto>();

            return Ok(_mapper.Map<IEnumerable<PieDto>>(piesFromRepo));
        }

        [HttpGet("{pieId}")]
        public IActionResult GetPie(Guid pieId)
        {
            var pieFromRepo = _piesRepository.GetPie(pieId);

            if (pieFromRepo == null)
            {
                return NotFound();
            }

            return Ok(_mapper.Map<PieDto>(pieFromRepo));
        }
    }
}
