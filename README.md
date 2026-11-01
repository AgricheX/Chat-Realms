# ChatRealms

Let your Discord server and your Twitch chat play inside your WorldBox world.

Players create units, join kingdoms, forge gear, found religions and clans, and send armies to war, all from Discord (and Twitch chat), while you watch it happen live in your game.

English first, português do Brasil logo abaixo.

---

## English

### What you need

- WorldBox with the **NML (Neo Mod Loader)** installed
- The ChatRealms mod, installed like any other NML mod
- A Discord server where you have the **Manage Server** permission
- The ChatRealms bot added to that server: **https://discord.com/oauth2/authorize?client_id=1561216083728277524**

Twitch is optional.

### Linking your game to your Discord server

You only do this once per world.

1. **Add the bot to your server** with the invite link above.
2. In any channel of your server, type `/pair generate`. The bot answers only to you and shows two things: the **Relay URL** and the **pairing code**. Keep this message private.
3. **Open WorldBox** and load your world. Open the mod settings and find the group **ChatRealms - Discord**.
4. Turn on **Discord relay enabled**.
5. Paste the address into **Relay URL (wss://...)**, and the code into **Pairing code**. Relay URL: `wss://emily-bot.duckdns.org/relay`
6. Back on Discord, type `/pair status`. It should say your server is paired. The mod status also changes to paired.

Your server is now connected to that world. A server is linked to one world at a time. To switch worlds or cut the connection, use `/pair revoke` and pair again.

### First steps for players

Anyone on the server can try these in the channels you allow:

- `/help` opens a visual guide. A lock marks what your rank does not unlock yet.
- `/points` shows your action points. Almost every action costs points, and they come back over time.
- `/kingdom join` puts you in a kingdom of the world.
- `/unit create <name>` creates your own unit in the capital of your kingdom.
- `/unit info` shows your unit. From there you can talk, heal, train, explore, work, patrol, fight and more.
- `/resources gather`, `/forge create` and `/equip unit` let you gather resources, forge real WorldBox gear and equip it.
- `/view unit` sends a real screenshot of your unit from the game.
- `/language` changes the language of your replies (English or Portuguese).

What you can do depends on your unit's rank in the game: citizen, soldier, general, city leader or king. Kings can ask for peace, declare war, make alliances and hold royal events.

### Tuning the game side (host only)

In the mod settings, group **ChatRealms - Discord**:

- **Allow unit roleplay actions** turns unit control on or off.
- **Allow wars declared from Discord** and **Allow destructive actions from Discord** control the dangerous stuff. Destructive actions are off by default.
- **Cooldown per user**, **Actions per minute** and **Blocked words** keep the chat in check.
- **Allow screenshots** lets players request screenshots (the camera moves briefly). **Hide the game interface in screenshots** hides the UI in them.
- **Apply RP Rules** applies the roleplay rules once when a world is first opened: no terraforming, diplomacy, rebellions, magic rites, monolith or other disasters. Natural disasters stay on.
- **Allow Insecure TLS** is only for when the connection fails because of a certificate problem. Leave it off otherwise.

Everything that happens in your world needs both layers to agree: your mod settings and the Discord server's own `/config`.

### Tuning the Discord side (server owner)

- `/config` lets the server owner decide roles, wars, point recovery, the maximum points and the notice channel.
- `/channels allow <group> <channel or category>` limits where each group of commands works. `/channels list` shows the rules. A group with no rule works everywhere.

### Twitch chat (optional)

1. On Discord, someone with Manage Server uses `/twitch channel <name>` to connect a Twitch channel to the server's world.
2. Give the bot account moderator in that channel.
3. Viewers play with `!` commands, such as `!kingdom join Alpha` or `!help`.
4. To use the same unit on Twitch and Discord, a player types `/link generate` on Discord and sends `!link CODE` in the Twitch chat.

The mod also runs Twitch polls that show up inside the game.

### Global War (extra, Discord only)

A separate shared world that lives only on the bot. Each Discord server is a realm on a big map. Players craft, trade, run adventures and fight wars against other servers. The commands all start with `gw-`, for example `/gw-realm found`, `/gw-hero join` and `/gw-world help`. It has no connection to your WorldBox world.

### Troubleshooting

- **The game never says paired.** Check that **Discord relay enabled** is on, that **Relay URL** starts with `wss://` and ends with `/relay`, and that the pairing code was copied without spaces. Generate a new one with `/pair generate` if in doubt.
- **A command says the app did not respond.** The bot may be offline for a moment. Try again in a minute.
- **A command says it cannot be used here.** The server owner limited that command group to other channels with `/channels`.
- **Nothing happens in the game.** Check **Allow unit roleplay actions** and that your world is the one currently paired in `/pair status`.

Never post your pairing code in public channels. If it leaks, run `/pair revoke` and generate a new one.

---

## Português (Brasil)

### O que você precisa

- WorldBox com o **NML (Neo Mod Loader)** instalado
- O mod ChatRealms, instalado como qualquer outro mod do NML
- Um servidor do Discord onde você tenha a permissão **Gerenciar Servidor**
- O bot do ChatRealms adicionado ao servidor: **https://discord.com/oauth2/authorize?client_id=1561216083728277524**

A Twitch é opcional.

### Ligando o jogo ao seu servidor do Discord

Você faz isso uma vez por mundo.

1. **Adicione o bot ao servidor** com o link de convite acima.
2. Em qualquer canal, digite `/parear gerar`. O bot responde só para você e mostra duas coisas: a **URL do relé** e o **código de pareamento**. Mantenha essa mensagem em segredo.
3. **Abra o WorldBox** e carregue o seu mundo. Abra as configurações do mod e procure o grupo **ChatRealms - Discord**.
4. Ligue **Discord relay enabled**.
5. Cole o endereço em **Relay URL (wss://...)** e o código em **Pairing code**. Relay URL: `wss://emily-bot.duckdns.org/relay`
6. De volta ao Discord, digite `/parear status`. Ele deve dizer que o servidor está pareado. O status no mod também muda para pareado.

Pronto, o seu servidor está ligado àquele mundo. Um servidor fica ligado a um mundo por vez. Para trocar de mundo ou cortar a ligação, use `/parear revogar` e pareie de novo.

### Primeiros passos para os jogadores

Qualquer pessoa do servidor pode testar isto nos canais que você liberar:

- `/ajuda` abre um guia visual. O cadeado marca o que o seu cargo ainda não libera.
- `/pontos` mostra os seus pontos de ação. Quase tudo custa pontos, e eles voltam com o tempo.
- `/reino entrar` coloca você em um reino do mundo.
- `/unidade criar <nome>` cria a sua unidade na capital do seu reino.
- `/unidade info` mostra a sua unidade. Daí você pode falar, curar, treinar, explorar, trabalhar, patrulhar, lutar e muito mais.
- `/recursos coletar`, `/forja criar` e `/equipar unidade` servem para coletar recursos, forjar equipamentos de verdade do WorldBox e equipar.
- `/ver unidade` envia um print real da sua unidade no jogo.
- `/idioma` muda o idioma das respostas (inglês ou português).

O que você pode fazer depende do cargo da sua unidade no jogo: cidadão, soldado, general, líder de cidade ou rei. O rei pode pedir paz, declarar guerra, fazer alianças e promover eventos reais.

### Ajustes do lado do jogo (só quem hospeda)

Nas configurações do mod, grupo **ChatRealms - Discord**:

- **Allow unit roleplay actions** liga ou desliga o controle de unidades.
- **Allow wars declared from Discord** e **Allow destructive actions from Discord** controlam o que é perigoso. As ações destrutivas vêm desligadas.
- **Cooldown per user**, **Actions per minute** e **Blocked words** mantêm o chat sob controle.
- **Allow screenshots** deixa os jogadores pedirem prints (a câmera se move por um instante). **Hide the game interface in screenshots** esconde a interface neles.
- **Apply RP Rules** aplica uma única vez as regras de roleplay quando o mundo é aberto pela primeira vez: sem terramorfismo, diplomacia, rebeliões, ritos mágicos, monólito ou outros desastres. Os desastres naturais continuam ligados.
- **Allow Insecure TLS** é só para quando a conexão falhar por problema de certificado. Deixe desligado.

Tudo o que acontece no seu mundo precisa da permissão das duas camadas: as configurações do mod e o `/config` do servidor do Discord.

### Ajustes do lado do Discord (dono do servidor)

- `/config` deixa o dono decidir cargos, guerras, recuperação de pontos, máximo de pontos e canal de avisos.
- `/canais permitir <grupo> <canal ou categoria>` limita onde cada grupo de comandos funciona. `/canais listar` mostra as regras. Um grupo sem regra funciona em qualquer lugar.

### Chat da Twitch (opcional)

1. No Discord, quem tem Gerenciar Servidor usa `/twitch canal <nome>` para ligar um canal da Twitch ao mundo do servidor.
2. Dê moderador à conta do bot nesse canal.
3. Os espectadores jogam com comandos `!`, como `!reino entrar Alfa` ou `!ajuda`.
4. Para usar a mesma unidade na Twitch e no Discord, o jogador digita `/vincular gerar` no Discord e manda `!vincular CODIGO` no chat da Twitch.

O mod também faz votações da Twitch que aparecem dentro do jogo.

### Guerra Global (extra, só no Discord)

Um mundo compartilhado e separado, que vive só no bot. Cada servidor do Discord é um reino em um mapa grande. Os jogadores criam itens, negociam, fazem aventuras e guerreiam contra outros servidores. Os comandos começam com `gw-`, como `/gw-realm found`, `/gw-hero join` e `/gw-world help`. Ele não tem ligação com o seu mundo do WorldBox.

### Problemas comuns

- **O jogo nunca mostra pareado.** Confira se **Discord relay enabled** está ligado, se a **Relay URL** começa com `wss://` e termina com `/relay`, e se o código foi copiado sem espaços. Gere outro com `/parear gerar` se tiver dúvida.
- **O comando diz que o aplicativo não respondeu.** O bot pode estar fora do ar por um instante. Tente de novo em um minuto.
- **O comando diz que não pode ser usado aqui.** O dono do servidor limitou esse grupo a outros canais com `/canais`.
- **Nada acontece no jogo.** Confira **Allow unit roleplay actions** e se o seu mundo é o que está pareado em `/parear status`.

Nunca poste o código de pareamento em canais públicos. Se ele vazar, use `/parear revogar` e gere um novo.