# Projeto Banco — API com Mensageria

> **FIAP | Engenharia de Software 3ESR | 2026**  
> Atividade Avaliativa — Backend de banco digital com .NET 8, Oracle, RabbitMQ e OpenTelemetry.

---

## 1. Identificação

| Nome Completo | RM |
|---|---|
| *(Integrante 1 — preencher)* | RM000001 |
| *(Integrante 2 — preencher)* | RM000002 |
| *(Integrante 3 — preencher)* | RM000003 |

---

## 2. Produtos Bancários Implementados

### Produto 1 — Máquina de Cartão (`MaquinaDeCartao`)
Contratação de terminal POS para aceitar pagamentos com cartão.

**Regra de negócio (extra dupla/trio):**
- PF: score de crédito mínimo **400** para aprovação.
- PJ: setor de atuação não pode ser CASSINO/APOSTAS (sem convênio).
- **Taxa MDR variável por setor:** VAREJO 1,5% · SERVIÇOS 2,0% · TECNOLOGIA 2,2% · outros 2,5%.

### Produto 2 — Empréstimo (`Emprestimo`)
Crédito pessoal e empresarial com prazo de até 60 meses.

**Regra de negócio (extra dupla/trio):**
- PF: score mínimo **600**. Taxa de juros mensal calculada pelo score:
  - ≥ 900 → 0,99% / ≥ 800 → 1,29% / ≥ 700 → 1,59% / ≥ 600 → 1,99%
- PJ: setor de alto risco (CONSTRUÇÃO, AGRICULTURA) tem limite máximo de R$50.000.
- Valor entre R$1.000 e R$100.000. Prazo entre 1 e 60 meses.

### Produto 3 — Receber Salário *(no diagrama, sem API completa — trio)*
Domicílio bancário para recebimento de salário via empresa conveniada.

---

## 3. Decisão de Modelagem de Filas

**Estratégia escolhida:** Exchange Topic (`banco.contratacoes`) com 3 filas vinculadas.

| Routing Key | Fila |
|---|---|
| `MAQUINA` | `contratacao.maquina` |
| `EMPRESTIMO` | `contratacao.emprestimo` |
| `SALARIO` | `contratacao.salario` |

**Justificativa e trade-off:**
- **Vantagem vs. fila única:** consumidores independentes por tipo de produto; escalar o consumer de Empréstimo sem afetar Máquina.
- **Vantagem vs. N filas sem exchange:** routing centralizado no broker; adicionar novo produto = novo binding, sem código extra no Producer.
- **Desvantagem:** complexidade levemente maior que fila única. Aceitável dado o escopo de trio com 2 produtos implementados.

---

## 4. Diagrama de Classes

> Arquivo fonte: [`docs/diagrama-classes.puml`](docs/diagrama-classes.puml)  
> Renderizar em [PlantUML Online](https://www.plantuml.com/plantuml/uml) ou extensão VS Code.

```
+-------------------+          +---------------------+
|     Agencia       |1       N |      Cliente        | <<abstract>>
|-------------------|<---------|---------------------|
| Id                |          | Id                  |
| Nome              |          | Nome, Email, Tel    |
| Codigo (unique)   |          | DataCadastro        |
| Cidade, Estado    |          | AgenciaId (FK)      |
+-------------------+          +---------------------+
                                    /\        /\
                        PF ─────────            ───────── PJ
                   +-------------+              +------------------+
                   | CPF (unique)|              | CNPJ (unique)    |
                   | DataNasc.   |              | RazaoSocial      |
                   | ScoreCredito|              | SetorAtuacao     |
                   +-------------+              +------------------+

+-------------------+          +--------------------+
|    Contratacao    |N       1 |      Produto       | <<abstract>>
|-------------------|--------->|--------------------|
| Id                |          | Id, Nome, Descricao|
| Status (enum)     |          | Ativo              |
| DataSolicitacao   |          +--------------------+
| DataProcessamento |                  /\
| MotivoRecusa      |       ┌──────────┼──────────┐
| ValorSolicitado   |  MaquinaDeCartao  Emprestimo  ReceberSalario
| PrazoMeses        |  (TaxaMDR)       (ValorMin   (EmpresaConv.)
| TaxaAplicada      |                   TaxaJuros)
+-------------------+
```

---

## 5. Como Rodar Localmente

### Pré-requisitos
- .NET 8.0 SDK
- Docker + Docker Compose
- Credenciais Oracle FIAP (RM individual)

### 1. Infraestrutura (RabbitMQ + Jaeger)

```bash
docker-compose up -d
```

Serviços disponíveis:
- RabbitMQ Management: http://localhost:15672 (guest/guest)
- Jaeger UI: http://localhost:16686

### 2. Configurar connection string Oracle

Edite `src/ProjetoBanco.API/appsettings.json`:
```json
"ConnectionStrings": {
  "Oracle": "User Id=RM000000;Password=SUA_SENHA;Data Source=oracle.fiap.com.br:1521/ORCL"
}
```

### 3. Criar banco (migrations)

```bash
cd src/ProjetoBanco.API
dotnet ef migrations add InitialCreate
dotnet ef database update
```

> **Nota:** a migration `20260101000000_InitialCreate.cs` é um esqueleto de referência.  
> O EF Core regenera automaticamente ao rodar `dotnet ef migrations add InitialCreate`.

### 4. Rodar a API

```bash
dotnet run --project src/ProjetoBanco.API
```

Swagger disponível em: http://localhost:5000

---

## 6. Endpoints com Exemplos

### POST /api/agencias
```json
// Request
{ "nome": "Agência Paulista", "codigo": "0001", "cidade": "São Paulo", "estado": "SP" }

// Response 201
{ "id": 1, "nome": "Agência Paulista", "codigo": "0001", "cidade": "São Paulo", "estado": "SP" }
```

### POST /api/clientes/pf
```json
// Request
{
  "nome": "João Silva", "email": "joao@email.com", "telefone": "11999990000",
  "cpf": "123.456.789-09", "dataNascimento": "1990-01-15", "agenciaId": 1, "scoreCredito": 750
}

// Response 201
{
  "id": 1, "tipo": "PF", "nome": "João Silva", "cpf": "123.456.789-09",
  "scoreCredito": 750, "agenciaId": 1, "agenciaNome": "Agência Paulista"
}
```

### POST /api/clientes/pj
```json
// Request
{
  "nome": "Loja ABC", "email": "contato@abc.com", "telefone": "1133330000",
  "cnpj": "12.345.678/0001-90", "razaoSocial": "ABC Comércio Ltda",
  "setorAtuacao": "VAREJO", "agenciaId": 1
}
```

### POST /api/contratacoes — Máquina de Cartão
```json
// Request
{ "clienteId": 1, "produtoId": 1 }

// Response 202 Accepted
{
  "id": 1, "clienteId": 1, "clienteNome": "João Silva",
  "produtoId": 1, "tipoProduto": "MaquinaDeCartao",
  "status": "PENDENTE", "dataSolicitacao": "2026-05-11T10:00:00Z"
}
```

### POST /api/contratacoes — Empréstimo
```json
// Request
{ "clienteId": 1, "produtoId": 2, "valorSolicitado": 15000.00, "prazoMeses": 24 }

// Response 202 Accepted
{
  "id": 2, "tipoProduto": "Emprestimo", "status": "PENDENTE",
  "valorSolicitado": 15000.00, "prazoMeses": 24
}
```

### GET /api/contratacoes/{id}
```json
// Response 200 (após Consumer processar)
{
  "id": 1, "status": "APROVADA", "taxaAplicada": 1.59,
  "dataProcessamento": "2026-05-11T10:00:05Z"
}
```

### GET /health
```json
{ "status": "Healthy", "entries": { "oracle-db": { "status": "Healthy" } } }
```

---

## 7. Executar Testes

```bash
dotnet test src/ProjetoBanco.Tests --logger "console;verbosity=detailed"
```

> *(Inserir print do resultado aqui após executar)*

**Cobertura dos fluxos críticos:**
- ✅ Cadastro PF com validação CPF duplicado
- ✅ Cadastro PJ com validação CNPJ duplicado
- ✅ Vincular cliente a agência inexistente → 404
- ✅ Solicitação de contratação válida → 202 + publicação na fila
- ✅ Contratação para cliente inexistente → 404
- ✅ Consulta de status após processamento
- ✅ Empréstimo sem campos obrigatórios → 400
- ✅ Health check → 200
- ✅ Regras de negócio (score, MDR, juros) — testes unitários

---

## 8. Painel RabbitMQ

> *(Inserir print do painel http://localhost:15672 mostrando filas e mensagens processadas)*

Filas esperadas após primeira contratação:
- `contratacao.maquina`
- `contratacao.emprestimo`
- `contratacao.salario`

---

## 9. Swagger com Contratação Aprovada

> *(Inserir print do Swagger em http://localhost:5000 com GET /api/contratacoes/{id} retornando status APROVADA)*

---

## Arquitetura

```
┌──────────────────────────────────────────────────────────────────┐
│                         ASP.NET Core API                         │
│                                                                  │
│  ContratacoesController ──publishes──> RabbitMQPublisher         │
│         │                               │                        │
│         │ 202 Accepted                  │ Exchange: banco.contratacoes (topic)
│         ▼                               ▼                        │
│    AppDbContext                  ┌─────────────┐                 │
│    (Oracle EF Core)              │  contratacao│                 │
│                                  │  .maquina   │                 │
│  ContratacaoConsumerService ◄────│  .emprestimo│                 │
│  (BackgroundService)             │  .salario   │                 │
│         │ ACK manual             └─────────────┘                 │
│         │                                                        │
│         └──> RegrasProdutoService ──> AppDbContext (update)      │
└──────────────────────────────────────────────────────────────────┘
         │                                    │
   Oracle FIAP                          OpenTelemetry
   (oracle.fiap.com.br)                 → Jaeger (localhost:16686)
                                        → Console
```
