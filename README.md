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
- **Importar** conexões (botão ao lado de "Nova conexão"), com revisão antes de adicionar. Itens já
  cadastrados ficam desmarcados. Fontes aceitas:
  - **backup do RemoteMan** (`.json`), mantendo os grupos;
  - arquivos **`.rdp`** (vários de uma vez): host, porta, usuário/domínio, resolução, monitores, área de
    transferência, unidades, sessão admin e RD Gateway;
  - **`~/.ssh/config`** ou outro arquivo no mesmo formato: `Host`, `HostName`, `User`, `Port`,
    `IdentityFile`, `ProxyJump` e `LocalForward`/`RemoteForward`/`DynamicForward`, respeitando os
    padrões de `Host *`;
  - **sessões SSH do PuTTY** (chaves `.ppk` não são importadas: converta no PuTTYgen).

  Senhas nunca são importadas.
- **Exportar** (mesmo botão): backup do RemoteMan em `.json` (todas as conexões ou um grupo, pelo menu do
  grupo), arquivos `.rdp` das conexões RDP e perfis SSH no formato do `ssh config`. Senhas nunca são
  exportadas.
- **Conexão rápida** (**Ctrl+K** em qualquer tela): digite parte do nome, host, grupo ou usuário, escolha
  com as setas e conecte com Enter. Favoritos e conexões recentes aparecem primeiro.
- **Lista de conexões** agrupada, com busca por nome, host, grupo ou usuário. O menu de cada grupo
  (⋯ ou clique direito) permite **conectar a todas** de uma vez.
- **Credenciais compartilhadas** (em Configurações): usuário e senha cadastrados uma vez e usados por
  várias conexões. Trocar a senha vale para todas.
- **Detalhes da conexão** em modo leitura, com botões **Conectar** e **Editar**. No modo edição, as
  alterações são salvas automaticamente (Ctrl+S grava na hora).
- **Configurações**: efeito de fundo da janela (**Mica**, **Mica Alt** ou **Acrílico** com desfoque, com
  fallback automático onde o Windows não suporta), credenciais compartilhadas, monitoramento,
  cliente SFTP preferido, verificação de atualizações, usuário do Windows,
  OpenSSH, Windows Terminal e caminho do arquivo de dados.
- **Monitoramento contínuo** (opcional): verifica o status a cada 1, 5, 15 ou 30 minutos e mostra uma
  notificação do Windows quando um servidor favorito cai ou volta. Funciona com o app aberto.
- **Aviso de nova versão**: ao abrir (no máximo uma vez por dia), o app consulta o último release no
  GitHub e, se houver versão nova, oferece baixar o instalador (ou o zip portátil, se for o caso).
- Em todas as listas, clicar numa conexão abre os detalhes e o botão ▶ conecta. Na tela Conexões, o
  duplo clique também conecta. O clique direito abre um menu com **Conectar** e, em conexões SSH,
  **Abrir SFTP**.
- **RDP**
  - Abre o `mstsc` com um arquivo `.rdp` gerado na hora.
  - Login automático com a senha salva (credencial `TERMSRV/host`).
  - Opção de usar o usuário do Windows da sessão atual.
  - Tela cheia ou resolução fixa, vários monitores, área de transferência, unidades locais e sessão `/admin`.
  - RD Gateway, com opção de ignorar para endereços locais e de usar as mesmas credenciais.
- **SSH**
  - Abre uma nova aba no Windows Terminal ou, se ele não estiver instalado, uma janela do `ssh.exe`.
  - Suporta porta personalizada, chave privada (`-i`), jump host / ProxyJump (`-J`) e túneis local
    (`-L`), remoto (`-R`) e SOCKS (`-D`), com aviso de porta local já em uso.
- **SFTP** (conexões SSH)
  - **Abrir SFTP** abre um cliente gráfico já conectado, reaproveitando host, porta, usuário, chave,
    jump host e credencial cadastrados.
  - Clientes suportados: **WinSCP**, **FileZilla** e **Bitvise SSH Client**, detectados automaticamente.
    O preferido é escolhido em Configurações; sem nenhum instalado, o app mostra onde baixar.

    | Cliente | Chave privada | Jump host |
    |---|---|---|
    | WinSCP | só `.ppk` (usa `chave.ppk` ao lado da chave OpenSSH, se existir) | um servidor intermediário |
    | FileZilla | configurada no próprio FileZilla | não suporta |
    | Bitvise | gerenciador de chaves do Bitvise | não suporta por parâmetro |

    Quando o cliente não aceita a chave ou o jump host, o app avisa ao abrir.
- **Favoritos**, contador de uso, duplicar e excluir conexões, notas livres por conexão.

## Requisitos

- Windows 10 versão 1809 ou superior (recomendado Windows 11).
- Para compilar, uma destas opções:
  - [.NET 10 SDK](https://dotnet.microsoft.com/download), para compilar pela linha de comando;
  - Visual Studio 2022/2026 com a carga de trabalho **Desenvolvimento de aplicativos do Windows**.
- Para conexões SSH: o cliente OpenSSH do Windows (já vem instalado no Windows 10/11 atual).
  O Windows Terminal é opcional.
- Para **Abrir SFTP**: WinSCP, FileZilla ou Bitvise SSH Client (opcional).

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
| `release.yml` | Chamado pelo bump ou push de tag `v*` | Gera o instalador e o zip portátil (x64 e ARM64) e cria o GitHub Release com as notas do CHANGELOG |

Cada release traz, por plataforma:

- `RemoteMan-Setup-X.Y.Z-x64.exe` (ou `-arm64`): **instalador**. Instala por usuário, sem pedir
  administrador, em `%LOCALAPPDATA%\Programs\RemoteMan`, cria atalho no Menu Iniciar e atualiza por cima da
  versão anterior. Gerado com [Inno Setup](https://jrsoftware.org/isinfo.php) a partir de
  `installer/RemoteMan.iss`.
- `RemoteMan-X.Y.Z-x64-portable.zip`: versão **portátil**. Basta extrair e abrir o `RemoteMan.exe`.

> O WinUI 3 não funciona com `PublishSingleFile` (o app quebra ao iniciar), por isso não há um `.exe`
> único sem instalação. Os executáveis não são assinados, então o SmartScreen pode exibir um aviso.

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
| Preferências | `%LOCALAPPDATA%\RemoteMan\settings.json` |
| Credenciais compartilhadas (sem senhas) | `%LOCALAPPDATA%\RemoteMan\credentials.json` |
| Senhas | Gerenciador de Credenciais do Windows, como `RemoteMan/<id da conexão>` ou `RemoteMan/cred/<id da credencial>` |
| Credencial usada pelo `mstsc` | Gerenciador de Credenciais do Windows, como `TERMSRV/<host>` |
| Arquivos `.rdp` temporários | `%TEMP%\RemoteMan\` (não contêm senha) |

As senhas são protegidas pelo DPAPI do usuário do Windows: só a sua conta, nesta máquina, consegue lê-las.

## Comportamentos importantes

- **Senha em SSH:** o `ssh.exe` não aceita senha por argumento. Ao conectar uma sessão SSH sem chave privada,
  o RemoteMan copia a senha salva para a área de transferência, fora do histórico do Win+V. Basta colar no
  terminal com o botão direito.
- **Senha no SFTP:** nunca é passada na linha de comando do cliente (ficaria visível na lista de processos).
  Se a chave não for repassada ao cliente, a senha salva vai para a área de transferência, como no SSH.
- **Usuário do Windows no RDP:** o login só é automático se a política "Permitir delegação de credenciais
  padrão" incluir `TERMSRV/*`. Normalmente quem configura essa política é o administrador do domínio. Sem
  ela, o `mstsc` pede a senha com o usuário já preenchido.
- **Credencial `TERMSRV/host`:** ao conectar via RDP com senha salva, a credencial fica no Windows depois da
  conexão, como acontece com o "Lembrar de mim" do próprio `mstsc`.
- **Status online/offline:** o dashboard testa a porta TCP de cada servidor (até 16 ao mesmo tempo, limite
  de 2 s cada). O teste roda ao abrir o Início ou a tela Conexões se a última verificação tiver mais de
  1 minuto, ou pelo botão **Verificar status** (⟳ na tela Conexões). Isso gera tráfego de rede para os
  servidores cadastrados. Em conexões com jump host, o teste é feito direto no servidor de destino, que
  costuma não responder de fora: o status "offline" pode não refletir a realidade.
- **Verificação de atualizações:** consulta `api.github.com` enviando apenas o nome e a versão do app
  (User-Agent). Pode ser desligada em Configurações.

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
│   ├── ImportDialog         # revisão das conexões encontradas antes de importar
│   ├── QuickConnectDialog   # conexão rápida (Ctrl+K)
│   ├── CredentialDialog     # cria/edita credencial compartilhada
│   └── SettingsPage         # ambiente e informações do app
├── Controls/
│   ├── ConnectionItem       # linha padrão de conexão usada em todas as listas
│   └── FieldRow             # linha "rótulo · valor · ação" dos cards
├── ViewModels/
│   ├── MainViewModel        # conexões, seleção, edição, salvamento e status (CommunityToolkit.Mvvm)
│   ├── DashboardViewModel   # favoritos, recentes e resumo do Início
│   ├── ConnectionGroup      # agrupamento da lista
│   └── ImportItem           # linha da lista de importação
├── Models/
│   ├── Connection           # modelo de uma conexão
│   ├── ConnectionAddress    # interpreta o endereço digitado (host:porta, usuario@host…)
│   ├── ImportedConnection   # formato neutro de importação/exportação (sem senhas)
│   ├── SharedCredential     # credencial compartilhada entre conexões
│   ├── SftpClientKind       # clientes SFTP suportados
│   └── AppSettings          # preferências (efeito de fundo, cliente SFTP…)
├── Services/
│   ├── Importers/           # leitura/escrita de .rdp, ssh config, PuTTY e backup .json
│   ├── ImportSources        # lê os arquivos e o registro e chama os parsers
│   ├── ConnectionStore      # leitura/gravação do connections.json
│   ├── SettingsStore        # leitura/gravação do settings.json
│   ├── SharedCredentialStore # leitura/gravação do credentials.json
│   ├── RdpFileBuilder       # monta o conteúdo do .rdp (inclui RD Gateway)
│   ├── SshTunnels           # túneis -L/-R/-D
│   ├── StatusTransitions    # detecta quedas e retornos (monitoramento)
│   ├── Notifier             # notificações do Windows
│   ├── CredentialStore      # Gerenciador de Credenciais (advapi32)
│   ├── RdpLauncher          # gera o .rdp e abre o mstsc
│   ├── SshLauncher          # abre o ssh no Windows Terminal ou console
│   ├── SshArguments         # monta os argumentos do ssh (-p, -i, -J)
│   ├── SftpLauncher         # detecta os clientes SFTP instalados e abre o escolhido
│   ├── SftpArguments        # monta os argumentos do WinSCP, FileZilla e Bitvise
│   ├── QuickSearch          # busca e ordenação da conexão rápida
│   ├── UpdateChecker        # consulta o último release no GitHub
│   ├── UpdateRelease        # compara versões e escolhe o arquivo de atualização
│   └── ReachabilityChecker  # teste de porta TCP
└── Helpers/
    └── Ui                   # funções usadas nos x:Bind da interface
```

O ícone é gerado por `tools/generate_icon.py` (requer Pillow). Para alterá-lo, edite o script e rode
`python tools/generate_icon.py`; ele recria `RemoteMan/Assets/AppIcon.ico` (16 a 256 px) e `AppIcon.png`.
