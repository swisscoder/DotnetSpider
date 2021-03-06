using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;
using DotnetSpider.Portal.Entity;
using DotnetSpider.Portal.Models.Docker;
using DotnetSpider.Portal.Models.DockerRepository;
using Microsoft.EntityFrameworkCore;

namespace DotnetSpider.Portal.Controllers
{
	public class DockerRepositoryController : Controller
	{
		private readonly ILogger _logger;
		private readonly PortalDbContext _dbContext;

		public DockerRepositoryController(PortalDbContext dbContext, ILogger<DockerRepositoryController> logger)
		{
			_logger = logger;
			_dbContext = dbContext;
		}

		[HttpGet("docker-repository/add")]
		public IActionResult Add()
		{
			return View();
		}

		[HttpPost("docker-repository/add")]
		public async Task<IActionResult> Add(AddRepositoryViewModel dto)
		{
			if (!ModelState.IsValid)
			{
				return View("Add", dto);
			}

			var items = await _dbContext.DockerRepositories.Where(x =>
				x.Name == dto.Name || x.Repository == dto.Repository).ToListAsync();

			if (items.Any(x => x.Name == dto.Name))
			{
				ModelState.AddModelError("Name", "名称已经存在");
			}

			if (items.Any(x => x.Repository == dto.Repository))
			{
				ModelState.AddModelError("Repository", "镜像仓储已经存在");
			}

			if (items.Any())
			{
				return View("Add", dto);
			}
			else
			{
				_dbContext.DockerRepositories.Add(new DockerRepository
				{
					Name = dto.Name,
					Registry = dto.Registry,
					Repository = dto.Repository,
					CreationTime = DateTime.Now
				});
				await _dbContext.SaveChangesAsync();
				return Redirect("/docker-repository");
			}
		}

		[HttpDelete("docker-repository/{id}")]
		public async Task<IActionResult> Delete(int id)
		{
			var item = await _dbContext.DockerRepositories.FirstOrDefaultAsync(x => x.Id == id);
			if (item != null)
			{
				_dbContext.DockerRepositories.Remove(item);
				await _dbContext.SaveChangesAsync();
			}

			return Redirect("/");
		}

		[HttpGet("docker-repository")]
		public async Task<IActionResult> Retrieve()
		{
			var list = await _dbContext.DockerRepositories.ToListAsync();
			return View(list);
		}

		[HttpPost("docker-repository/payload")]
		public async Task<IActionResult> Payload([FromBody] RepositoryPayload payload)
		{
			var repository =
				await _dbContext.DockerRepositories.FirstOrDefaultAsync(
					x => x.Repository == payload.Repository.Repo_Full_Name);
			if (repository != null)
			{
				if (payload.Push_Data.Tag == "latest")
				{
					_logger.LogWarning($"忽略仓库 {payload.Repository.Repo_Full_Name} 的 latest 版本镜像");
					return Ok();
				}

				var image = $"{repository.Registry}/{payload.Repository.Repo_Full_Name}:{payload.Push_Data.Tag}";
				if (!await _dbContext.DockerImages.AnyAsync(x =>
					x.Image == image))
				{
					_dbContext.DockerImages.Add(new DockerImage
					{
						Image = image,
						CreationTime = DateTime.Now,
						RepositoryId = repository.Id
					});

					await _dbContext.SaveChangesAsync();
					_logger.LogInformation($"镜像 {image} 添加成功");
				}
				else
				{
					_logger.LogInformation($"镜像 {image} 已经存在");
				}
			}
			else
			{
				_logger.LogWarning($"仓库 {payload.Repository.Repo_Full_Name} 未配置");
			}

			return Ok();
		}
	}
}