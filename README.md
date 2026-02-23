# Super-terminal-for-barotrauma
This is a work-in-progress Barotrauma mod designed to optimize performance by digitalizing item storage, significantly reducing the entity count and physical footprint of stored items.
(这是一个开发中的潜渊症 Mod，旨在通过将存储物品数据化来优化性能，显著减少存储物品的实体数量和物理占用。)

 Super Terminal | 超级终端
![Status](https://img.shields.io/badge/Status-Single--Player--Only-orange)
![Requirement](https://img.shields.io/badge/Requirement-LuaCS-blue)
![Game](https://img.shields.io/badge/Game-Barotrauma-green)
[**English**](#english) | [**中文说明**](#中文说明)
---
 中文说明
 项目简介
**Super Terminal** 是一个为《潜渊症》（Barotrauma）设计的高性能后台存储系统。它通过将物品数据存储在外部 XML 文件中，彻底解决了潜艇内物品过多导致的掉帧（FPS Drop）和空间占用问题。
 核心功能
*   **无限后台存储**：将所有物品存入后台数据库，极速提升游戏运行效率。
*   **深度兼容**：支持嵌套容器（套包），内部物品内容物、耐久度、电量均能完美保存。
*   **无冲突设计**：同种类但内容物不同的物品独立存储，互不干扰。
*   **极速存取**：配备专门的存储交互终端，实现流畅的物资存取体验。
 安装要求
*   必须安装 **[LuaForBarotrauma (LuaCS)](https://steamcommunity.com/sharedfiles/filedetails/?id=2559634234)**。
*   目前仅支持 **单人模式**。
 存档与修改
存档数据以 XML 格式存储在以下路径：
`%AppData%\Local\Daedalic Entertainment GmbH\Barotrauma`
文件前缀为：`super_terminal_`
---
 English
 Overview
**Super Terminal** is a high-performance background storage solution for *Barotrauma*. It offloads item data into external XML files, significantly reducing entity counts on the submarine to improve FPS and declutter your workspace.
 Key Features
*   **Background Data Storage**: Stores items into background data instead of physical entities, boosting performance.
*   **Nested Container Support**: Fully recognizes and preserves items inside bags, crates, and other containers.
*   **Conflict-Free**: Items of the same type with different contents/stats are stored independently without risk of data overwriting.
*   **Rapid Interaction**: Includes a dedicated memory module for fast item transfers.
 Prerequisites
*   **[LuaForBarotrauma (LuaCS)](https://steamcommunity.com/sharedfiles/filedetails/?id=2559634234)** is strictly required.
*   Currently functional in **Single Player** only.
 Data & Modification
You can find the raw storage data at:
`AppData\Local\Daedalic Entertainment GmbH\Barotrauma`
Look for files with the prefix: `super_terminal_`
---
CALL FOR HELP / 技术协助
> [!IMPORTANT]
> **EN:** I am looking for experienced modders/coders to help resolve the issues preventing this mod from working in Multiplayer. If you have experience with LuaCS synchronization, **plz refresh this mod and upload new mod on the workshop without contacting me.**
> 
> **CN:** 目前多人模式代码存在问题无法正常运行，诚邀各位技术大佬/Modder 协助解决多人模式的同步与适配问题。**如果你有更好的多人服务器方案请直接发布在创意工坊，不用联系我。**
---
 License
[Choose a License, e.g., MIT]
