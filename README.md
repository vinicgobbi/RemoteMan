# RemoteMan

Gerenciador de conexões **RDP** e **SSH** para Windows, feito em **WinUI 3** (.NET 10 + Windows App SDK).

O RemoteMan guarda seus servidores organizados por grupo e abre cada conexão com um clique. Para RDP, usa o
`mstsc`. Para SSH, usa o OpenSSH no Windows Terminal. As senhas ficam no Gerenciador de Credenciais do
Windows, nunca em arquivo.

## Funcionalidades

- **Dashboard inicial** com:
  - resumo das conexões (total, RDP, SSH e grupos);
  - última conexão realizada, com botão para reconectar;
  - conexões recentes, favoritas e mais usadas;
  - status online/offline de cada servidor (teste de porta TCP);
  - informações do ambiente: usuário do Windows, OpenSSH, Windows Terminal e caminho do arquivo de dados.
- **Lista de conexões** agrupada, com busca por nome, host, grupo ou usuário.
- **RDP**
  - Abre o `mstsc` com um arquivo `.rdp` gerado na hora.
  - Login automático com a senha salva (credencial `TERMSRV/host`).
  - Opção de usar o usuário do Windows da sessão atual.
  - Tela cheia ou resolução fixa, vários monitores, área de transferência, unidades locais e sessão `/admin`.
- **SSH**
  - Abre uma nova aba no Windows Terminal ou, se ele não estiver instalado, uma janela do `ssh.exe`.
  - Suporta porta personalizada e chave privada (`-i`).
- **Favoritos**, duplicar e excluir conexões, notas livres por conexão.

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
  de 2 s cada). O teste roda ao abrir o dashboard se a última verificação tiver mais de 1 minuto, ou pelo
  botão **Verificar status**. Isso gera tráfego de rede para os servidores cadastrados.

## Estrutura do projeto

```
RemoteMan/
├── App.xaml(.cs)            # inicialização e estado compartilhado
├── MainWindow.xaml(.cs)     # janela com barra de título e menu de navegação
├── Views/
│   ├── DashboardPage        # tela inicial
│   └── ConnectionsPage      # lista e editor de conexões
├── ViewModels/
│   ├── MainViewModel        # conexões, seleção, comandos (CommunityToolkit.Mvvm)
│   ├── DashboardViewModel   # dados do dashboard e verificação de status
│   └── ConnectionGroup      # agrupamento da lista
├── Models/
│   └── Connection           # modelo de uma conexão
├── Services/
│   ├── ConnectionStore      # leitura/gravação do connections.json
│   ├── CredentialStore      # Gerenciador de Credenciais (advapi32)
│   ├── RdpLauncher          # gera o .rdp e abre o mstsc
│   ├── SshLauncher          # abre o ssh no Windows Terminal ou console
│   └── ReachabilityChecker  # teste de porta TCP
└── Helpers/
    └── Ui                   # funções usadas nos x:Bind da interface
```
