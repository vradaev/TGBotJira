# TGBotJira

My Project is a tool that helps you helpesk team improve communication with the clients and allow to integrate your Telegram channels to JIRA service desk.

## Content

- [Description](#description)
- [Installation](#installation)
- [Contacts](#contacts)
- [ChangeLog](#changelog)

## Description

My Project is help to recived the client message in the telegram and open tickets in the JIRA service desk.

## Installation

1. Clone the repo:
    ```bash
    git clone https://github.com/vradaev/TGBotJira.git
    ```
2. Copy your config file from local to remote Ubuntu with Docker:
    ```bash
     scp -P 5000 /Users/vradaev/RiderProjects/CTHelpDesk/config.json vradaev@111.1.11.11:~/TGBotJira/
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
    docker logs -f jira-telegram-bot
    ```

## Contacts

If you have any questions, you can contact me on the TG: @vadimradaev


## ChangeLog

Все значимые изменения и обновления можно найти в [CHANGELOG.md](CHANGELOG.md).
