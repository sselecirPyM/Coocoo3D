# Coocoo3D
![image](https://user-images.githubusercontent.com/63526047/150717738-58eb5cfe-dc19-417d-b389-f8f35607a679.png)

一个CPU要求极低的MMD渲染器，支持DirectX12 和DXR实时光线追踪，具有可编程渲染管线。

(远古版本)视频[https://www.bilibili.com/video/BV1p54y127ig/](https://www.bilibili.com/video/BV1p54y127ig/)

## 基本功能
* 加载pmx、glTF模型
* 加载vmd动作
* 播放动画
* 录制图像序列

## 图形功能
* 可编程渲染管线
* 贴花
* 烘焙天空盒
* 后处理
* 光线追踪反射
* 屏幕空间反射(使用Hierarchical ZBuffer)
* 全局光照
* SSAO
* TAA

## 截图

全局光照：关
![屏幕截图 2022-03-14 213422](https://user-images.githubusercontent.com/63526047/158182829-b817ec09-e5fa-4f30-9753-3fd5f0d1a6bc.png)

全局光照：开
![屏幕截图 2022-03-14 213438](https://user-images.githubusercontent.com/63526047/158182978-0b84d0bf-99cd-489d-8522-6684d9cf48d7.png)

体积光：开
![屏幕截图 2022-03-14 213644](https://user-images.githubusercontent.com/63526047/158183360-0465767c-e416-4d1b-b342-56b2b14dcc4e.png)

光线追踪：开 镜面反射
![屏幕截图 2022-03-14 213859](https://user-images.githubusercontent.com/63526047/158183752-837d9481-96b8-4097-ae7a-1c15477a217e.png)

![屏幕截图 2022-03-17 131925](https://user-images.githubusercontent.com/63526047/158742418-dca992c7-bc91-4bdb-8569-0a541887cd5e.png)

贴花支持
![屏幕截图 2022-05-26 201224](https://user-images.githubusercontent.com/63526047/170485548-466c2199-ccdb-41fc-9c2f-a1930d77ce73.png)

## Radeon Prorender已移除
不再使用。