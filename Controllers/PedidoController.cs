using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PedidosNet.Data;
using PedidosNet.Domain;
using PedidosNet.DTOs;
using PedidosNet.Models;

namespace PedidosNet.Controllers;

[ApiController]
[Route("api/pedidos")]
public class PedidoController(
    AppDbContext context, 
    IPedidoFactory pedidoFactory) 
    : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var pedidos = await context.Pedidos
            .Include(p => p.Cliente)
            .Include(p => p.Itens)
            .AsNoTracking()
            .ToListAsync();

        return Ok(pedidos);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObterPorId(Guid id)
    {
        var pedido = await context.Pedidos
            .Include(p => p.Cliente)
            .Include(p => p.Itens)
            .FirstOrDefaultAsync(p => p.Id == id);

        return pedido is null ? NotFound() : Ok(pedido);
    }

    [HttpPost]
    public async Task<IActionResult> Criar(CreatePedidoRequest request)
    {
        var cliente = await context.Clientes.FindAsync(request.ClienteId);
        if (cliente is null)
            return BadRequest("Cliente não encontrado.");

        //Regra 1
        if(cliente.Bloqueado)
            return BadRequest("Cliente bloqueado não pode criar pedido.");

        var itens = new List<ItemPedido>();

        foreach (var itemRequest in request.Itens)
        {
            var produto = await context.Produtos.FindAsync(itemRequest.ProdutoId);
            if (produto is null)
                return BadRequest($"Produto {itemRequest.ProdutoId} não encontrado.");

            itens.Add(new ItemPedido
            {
                Id = Guid.NewGuid(),
                ProdutoId = produto.Id,
                NomeProduto = produto.Nome,
                Preco = produto.Preco,
                Quantidade = itemRequest.Quantidade
            });
        }

        var pedido = pedidoFactory.Criar(cliente, itens);

        // ⚠️ Regras de negócio direto no controller — o primeiro sinal amarelo
        // piscando no painel . A partir daqui, cada nova regra
        // (cliente corporativo, campanha, valor mínimo...) só engorda este método.
        if (cliente.Vip)
            pedido.Total -= pedido.Total * 0.10m;

        if (pedido.Total > 2000)
            pedido.Total -= pedido.Total * 0.05m;

        context.Pedidos.Add(pedido);
        await context.SaveChangesAsync();

        return Ok(pedido.Id);
    }
}
