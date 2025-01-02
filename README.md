# TGBotJira

My Project is a tool that helps you helpesk team improve communication with the clients and allow to integrate your Telegram channels to JIRA service desk.

## Content

- [Description](#description)
- [Installation](#installation)
- [Contacts](#contacts)
- [ChangeLog](#changelog)

## Description

My Project is help to recived the client message in the telegram channel and open tickets in the JIRA service desk.
The project is integrated with receiving the support team metrics dashboard from Superset and sends the metrics to a Telegram channel.

### Available commands 
**/addcleint nameofclient** - The command add a client and chat ID for interaction with the bot. 

**/dashboard** - The command sends a Superset dashboard with support team metrics. 

**/sos** - The command sends a notification to duty support employee.

**/cachereload** - The system command which reload order cache.

## Installation

1. Clone the repo:
    ```bash
    git clone https://github.com/vradaev/TGBotJira.git
    ```
2. Copy your config file from local to remote Ubuntu with Docker:
    ```bash
     scp -P 5000 /Users/login/RiderProjects/CTHelpDesk/config.json login@111.1.11.11:~/TGBotJira/
    ```
3. Open directory
    ```bash
    cd TGBotJira
    ```
4. Create in Docker image
    ```bash
    docker compose build
    ```
5. Run in Docker container
    ```bash
    docker compose up -d
    ```
6. Check status Docker container
    ```bash
    docker ps
    ```
7. Check logs 
    ```bash
    docker compose logs -f jira-telegram-bot
    ```

## Contacts

If you have any questions, please can contact me on the TG: @vadimradaev


## ChangeLog

All significant changes and updates can be found in [CHANGELOG.md](CHANGELOG.md)
