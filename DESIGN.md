---
name: MensLink 资料工作台
description: 沿用 Obsidian 原生主题的三栏资料核对界面
typography:
  title:
    fontFamily: var(--font-interface)
    fontSize: 24px
    fontWeight: 650
    lineHeight: 1.25
  body:
    fontFamily: var(--font-text)
    fontSize: 15px
    lineHeight: 1.9
rounded:
  small: 4px
spacing:
  small: 8px
  medium: 16px
  large: 24px
---

## Overview

用户选择三栏资料工作台。主任务是从来源筛选到检索命中，再核对固定版本与引用。使用 Obsidian 的界面语法；界面模式为 Operate，不另造网页品牌。

## Colors

主题是颜色权威：正文 `--text-normal`、次要说明 `--text-muted`、主面板 `--background-primary`、来源栏 `--background-secondary`。`--interactive-accent` 仅用于主操作和焦点；命中使用 `--text-highlight-bg`，选择使用 `--background-modifier-active-hover`。不将某次宿主计算颜色固化为全主题色值。

## Typography

界面继承 `--font-interface` 与宿主 small/smaller 字号。工作台标题 24px，资料标题 22px，正文片段 15px/1.9、最多72ch；身份、摘要与引用才用 `--font-monospace`。正文按纯文本保留换行，不执行资料中的 HTML、Markdown 或指令。

## Layout

大窗口三栏：来源、命中、详情。列宽比例约0.7:1:1.6，最小宽度180/250/340px。按工作台实际容器宽度响应：小于900px来源变顶栏；小于600px按来源→命中→详情纵排，滚动阅读，不能挤出横向溢出。

间距使用8/16/24px；同组紧凑，资料正文与引用区之间留出段落距离。工具栏始终能找到检索与重新连接。无结果、加载、离线和权限错误都在当前界面说明恢复动作。

## Elevation & Depth

使用主题表面和1px分隔线，不叠加自定义投影。结果是连续列表，不能套成多层卡片。

## Shapes

沿用宿主按钮和输入框。自有选择、引用框采用4px圆角；图标统一使用宿主 `setIcon` 的16px线性图标。

## Components

来源按钮筛选当前已返回命中；结果按钮通过 `aria-pressed` 标注选择。详情先 fetch 再呈现固定身份；复制前再检查可读性。加载标注 `aria-busy`，结果和错误用 live region反馈，焦点保留2px强调色边框。

唯一过渡为正文读取后的180ms背景淡出；尊重 `prefers-reduced-motion`。打开即显示工作内容，不编排页面入场。

## Do's and Don'ts

显示真实查询、定位和错误；清楚标识合成资料与开发预览。不给未实现的配对、授权、持久任务或发布状态放假按钮。用户审美评价和真实交互验收分别记录；主题兼容只能按实际覆盖声明。
