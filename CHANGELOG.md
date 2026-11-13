## v0.4.0 (2026-11-13)

### Feat

- **credenciais**: adiciona credenciais compartilhadas entre conexões
- **status**: adiciona monitoramento contínuo com notificações
- **ssh**: adiciona túneis SSH (port forwarding)
- **exportacao**: exporta conexões e importa backups do RemoteMan
- **rdp**: adiciona suporte a RD Gateway
- **conexoes**: permite conectar a todas as conexões de um grupo

## v0.3.0 (2026-11-13)

### Feat

- **conexoes**: adiciona conexão rápida com Ctrl+K
- **atualizacoes**: avisa quando há nova versão disponível
- **ssh**: adiciona suporte a jump host (ProxyJump)
- **importacao**: importa conexões de .rdp, ~/.ssh/config e PuTTY
- **ui**: permite escolher o efeito de fundo da janela

## v0.2.0 (2026-11-13)

### Feat

- **instalador**: adiciona instalador do Windows com Inno Setup

## v0.1.0 (2026-11-12)

### Feat

- **conexoes**: adiciona diálogo de nova conexão
- **ui**: adiciona ícone próprio do app
- **ui**: redesenha telas com modo leitura e salvamento automático
- **ui**: adiciona estilos comuns e componentes de linha
- **dashboard**: adiciona tela inicial com dashboard e menu de navegação
- **status**: adiciona teste de porta TCP dos servidores
- **conexoes**: adiciona favoritos e contador de uso
- **ui**: adiciona janela principal de gerenciamento de conexões
- **conexoes**: abre sessões RDP via mstsc e SSH via terminal
- **credenciais**: guarda senhas no Gerenciador de Credenciais do Windows
- **conexoes**: adiciona modelo e persistência das conexões

### Refactor

- **conexoes**: extrai interpretação do endereço para ConnectionAddress
- **ssh**: expõe detecção do OpenSSH e do Windows Terminal
