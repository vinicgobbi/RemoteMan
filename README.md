# RemoteMan

Gerenciador de conexões **RDP** e **SSH** para Windows, feito em **WinUI 3** (.NET 10 + Windows App SDK).

O RemoteMan guarda seus servidores organizados por grupo e abre cada conexão com um clique. Para RDP, usa o
`mstsc`. Para SSH, usa o OpenSSH no Windows Terminal. As senhas ficam no Gerenciador de Credenciais do
Windows, nunca em arquivo.

## Funcionalidades

- **Início** com resumo do status dos servidores (online/offline), favoritos e conexões recentes.
- **Nova conexão** (botão ou Ctrl+N) num diálogo curto: tipo, endereço, usuário/senha e grupo. Aceita
  `servidor:porta`, `usuario@servidor` e `DOMINIO\usuario`, avisa se o endereço já está cadastrado e
  permite **Criar e conectar** direto. As opções avançadas ficam em **Editar**.
- **Lista de conexões** agrupada, com busca por nome, host, grupo ou usuário.
- **Detalhes da conexão** em modo leitura, com botões **Conectar** e **Editar**. No modo edição, as
  alterações são salvas automaticamente (Ctrl+S grava na hora).
- **Configurações**: usuário do Windows, OpenSSH, Windows Terminal e caminho do arquivo de dados.
- Em todas as listas, clicar numa conexão abre os detalhes e o botão ▶ conecta. Na tela Conexões, o
  duplo clique também conecta.
- **RDP**
  - Abre o `mstsc` com um arquivo `.rdp` gerado na hora.
  - Login automático com a senha salva (credencial `TERMSRV/host`).
  - Opção de usar o usuário do Windows da sessão atual.
  - Tela cheia ou resolução fixa, vários monitores, área de transferência, unidades locais e sessão `/admin`.
- **SSH**
  - Abre uma nova aba no Windows Terminal ou, se ele não estiver instalado, uma janela do `ssh.exe`.
  - Suporta porta personalizada e chave privada (`-i`).
- **Favoritos**, contador de uso, duplicar e excluir conexões, notas livres por conexão.

## Requisitos

- Windows 10 versão 1809 ou superior (recomendado Windows 11).
- Para compilar, uma destas opções:
  - [.NET 10 SDK](https://dotnet.microsoft.com/download), para compilar pela linha de comando;
  - Visual Studio 2022/2026 com a carga de trabalho **Desenvolvimento de aplicativos do Windows**.
- Para conexões SSH: o cliente OpenSSH do Windows (já vem instalado no Windows 10/11 atual).
  O Windows Terminal é opcional.

## Como compilar e executar

Pela linha de comando:

```powershell
git clone https://github.com/vinicgobbi/RemoteMan.git
cd RemoteMan
dotnet run --project RemoteMan -p:Platform=x64
```

Pelo Visual Studio: abra `RemoteMan.sln`, selecione a plataforma **x64** e pressione **F5**.

O app é **não empacotado** (sem MSIX) e leva o Windows App SDK junto (*self-contained*). Por isso roda
direto do `.exe` gerado em `RemoteMan\bin\x64\<Configuração>\net10.0-windows10.0.19041.0\`.

> O repositório inclui um `nuget.config` apontando para o nuget.org, para o restore funcionar mesmo em
> máquinas sem fonte de pacotes configurada.

Para rodar os testes:

```powershell
dotnet test --project RemoteMan.Tests
```

## Versionamento e releases

Os commits seguem [Conventional Commits](https://www.conventionalcommits.org/pt-br/v1.0.0/) e a versão é
controlada pelo [Commitizen](https://commitizen-tools.github.io/commitizen/) (`.cz.toml`).

| Workflow | Quando roda | O que faz |
|---|---|---|
| `ci.yml` | Pull requests | Valida as mensagens de commit (`cz check`), roda os testes e compila x64/ARM64 |
| `bump.yml` | Push na `main` | Roda o CI; se houver `feat`, `fix`, `perf`, `refactor` ou `!`, faz o `cz bump` (versão no `.csproj`, `CHANGELOG.md` e tag `vX.Y.Z`) e chama o release |
| `release.yml` | Chamado pelo bump ou push de tag `v*` | Publica os `.zip` self-contained (x64 e ARM64) e cria o GitHub Release com as notas do CHANGELOG |

Enquanto a versão for `0.x`, `feat` sobe a *minor* e `fix`/`perf`/`refactor` sobem a *patch*. Commits
`docs`, `chore`, `ci`, `test` etc. não geram versão.

Para conferir localmente (requer `pip install commitizen`):

```powershell
cz check --rev-range origin/main..HEAD   # valida as mensagens dos seus commits
cz bump --dry-run                        # mostra qual seria a próxima versão
```

## Onde ficam os dados

| O quê | Onde |
|---|---|
| Conexões (sem senhas) | `%LOCALAPPDATA%\RemoteMan\connections.json` |
| Senhas | Gerenciador de Credenciais do Windows, como `RemoteMan/<id da conexão>` |
| Credencial usada pelo `mstsc` | Gerenciador de Credenciais do Windows, como `TERMSRV/<host>` |
| Arquivos `.rdp` temporários | `%TEMP%\RemoteMan\` (não contêm senha) |

As senhas são protegidas pelo DPAPI do usuário do Windows: só a sua conta, nesta máquina, consegue lê-las.

## Comportamentos importantes

- **Senha em SSH:** o `ssh.exe` não aceita senha por argumento. Ao conectar uma sessão SSH sem chave privada,
  o RemoteMan copia a senha salva para a área de transferência, fora do histórico do Win+V. Basta colar no
  terminal com o botão direito.
- **Usuário do Windows no RDP:** o login só é automático se a política "Permitir delegação de credenciais
  padrão" incluir `TERMSRV/*`. Normalmente quem configura essa política é o administrador do domínio. Sem
  ela, o `mstsc` pede a senha com o usuário já preenchido.
- **Credencial `TERMSRV/host`:** ao conectar via RDP com senha salva, a credencial fica no Windows depois da
  conexão, como acontece com o "Lembrar de mim" do próprio `mstsc`.
- **Status online/offline:** o dashboard testa a porta TCP de cada servidor (até 16 ao mesmo tempo, limite
  de 2 s cada). O teste roda ao abrir o Início ou a tela Conexões se a última verificação tiver mais de
  1 minuto, ou pelo botão **Verificar status** (⟳ na tela Conexões). Isso gera tráfego de rede para os
  servidores cadastrados.

## Estrutura do projeto

```
RemoteMan/
├── App.xaml(.cs)            # inicialização, estado compartilhado e estilos comuns
├── MainWindow.xaml(.cs)     # janela com barra de título e menu de navegação
├── Assets/
│   └── AppIcon.ico / .png   # ícone do app (.exe, janela e barra de título)
├── Views/
│   ├── DashboardPage        # tela inicial
│   ├── ConnectionsPage      # lista, detalhes e editor de conexões
│   ├── NewConnectionDialog  # diálogo de criação com os campos essenciais
│   └── SettingsPage         # ambiente e informações do app
├── Controls/
│   ├── ConnectionItem       # linha padrão de conexão usada em todas as listas
│   └── FieldRow             # linha "rótulo · valor · ação" dos cards
├── ViewModels/
│   ├── MainViewModel        # conexões, seleção, edição, salvamento e status (CommunityToolkit.Mvvm)
│   ├── DashboardViewModel   # favoritos, recentes e resumo do Início
│   └── ConnectionGroup      # agrupamento da lista
├── Models/
│   ├── Connection           # modelo de uma conexão
│   └── ConnectionAddress    # interpreta o endereço digitado (host:porta, usuario@host…)
├── Services/
│   ├── ConnectionStore      # leitura/gravação do connections.json
│   ├── CredentialStore      # Gerenciador de Credenciais (advapi32)
│   ├── RdpLauncher          # gera o .rdp e abre o mstsc
│   ├── SshLauncher          # abre o ssh no Windows Terminal ou console
│   └── ReachabilityChecker  # teste de porta TCP
└── Helpers/
    └── Ui                   # funções usadas nos x:Bind da interface
```

O ícone é gerado por `tools/generate_icon.py` (requer Pillow). Para alterá-lo, edite o script e rode
`python tools/generate_icon.py`; ele recria `RemoteMan/Assets/AppIcon.ico` (16 a 256 px) e `AppIcon.png`.
