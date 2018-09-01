# LockStepSimpleFramework-Client
unity帧同步游戏极简框架-客户端

**阅前提示:**
此框架为有帧同步需求的游戏做一个简单的示例,实现了一个精简的框架,本文着重讲解帧同步游戏开发过程中需要注意的各种要点,伴随框架自带了一个小的塔防sample作为演示.

目录:

[TOC]

#哪些游戏需要使用帧同步
如果游戏中有如下需求,那这个游戏的开发框架应该使用帧同步:
<li>多人实时对战游戏
<li>游戏中需要战斗回放功能
<li>游戏中需要加速功能
<li>需要服务器同步逻辑校验防止作弊

LockStep框架就是为了上面几种情况而设计的.

#如何实现一个可行的帧同步框架
主要确保以下三点来保证帧同步的准确性:
<li>可靠稳定的帧同步基础算法
<li>消除浮点数带来的精度误差
<li>控制好随机数

##<font size = 5>帧同步原理</font>
>相同的输入 + 相同的时机 = 相同的显示

客户端接受的输入是相同的，执行的逻辑帧也是一样的，那么每次得到的结果肯定也是同步一致的。为了让运行结果不与硬件运行速度快慢相关联，则不能用现实历经的时间(Time.deltaTime)作为差值阀值进行计算,而是使用固定的时间片段来作为阀值,这样无论两帧之间的真实时间间隔是多少,游戏逻辑执行的次数是恒定的,举例:
我们预设每个逻辑帧的时间跨度是1秒钟,那么当物理时间经过10秒后,逻辑便会运行10次,经过100秒便会运行100次,无论在运行速度快的机器上还是慢的机器上均是如此,不会因为两帧之间的跨度间隔而有所改变。
而渲染帧（一般为30到60帧），则是根据逻辑帧（10到20帧）去插值，从而得到一个“平滑”的展示,渲染帧只是逻辑帧的无限逼近插值,不过人眼一般无法分辨这种滞后性,因此可以把这两者理解为同步的.
>如果硬件的运行速度赶不上逻辑帧的运行速度,则有可能出现逻辑执行多次后,渲染才执行一次的状况,如果遇到这种情况画面就会出现卡顿和丢帧的情况.

##<font size = 5>帧同步算法</font>
##<font size = 4>基础核心算法</font>
下面这段代码为帧同步的核心逻辑片段:
```
m_fAccumilatedTime = m_fAccumilatedTime + deltaTime;

//如果真实累计的时间超过游戏帧逻辑原本应有的时间,则循环执行逻辑,确保整个逻辑的运算不会因为帧间隔时间的波动而计算出不同的结果
while (m_fAccumilatedTime > m_fNextGameTime) {

    //运行与游戏相关的具体逻辑
    m_callUnit.frameLockLogic();

    //计算下一个逻辑帧应有的时间
    m_fNextGameTime += m_fFrameLen;

    //游戏逻辑帧自增
    GameData.g_uGameLogicFrame += 1;
}

//计算两帧的时间差,用于运行补间动画
m_fInterpolation = (m_fAccumilatedTime + m_fFrameLen - m_fNextGameTime) / m_fFrameLen;

//更新渲染位置
m_callUnit.updateRenderPosition(m_fInterpolation);
```
##<font size = 4>渲染更新机制</font>
由于帧同步以及逻辑与渲染分离的设置,我们不能再去直接操作transform的localPosition,而设立一个虚拟的逻辑值进行代替,我们在游戏逻辑中,如果需要变更对象的位置,只需要更新这个虚拟的逻辑值,在一轮逻辑计算完毕后会根据这个值统一进行一轮渲染,这里我们引入了逻辑位置m_fixv3LogicPosition这个变量.
```
// 设置位置
// 
// @param position 要设置到的位置
// @return none
public override void setPosition(FixVector3 position)
{
    m_fixv3LogicPosition = position;
}
```
渲染流程如下:
![](https://img-blog.csdn.net/20180831094842873?watermark/2/text/aHR0cHM6Ly9ibG9nLmNzZG4ubmV0L3dhbnppMjE1/font/5a6L5L2T/fontsize/400/fill/I0JBQkFCMA==/dissolve/70)

只有需要移动的物体,我们才进行插值运算,不会移动的静止物体直接设置其坐标就可以了
```
//只有会移动的对象才需要采用插值算法补间动画,不会移动的对象直接设置位置即可
if ((m_scType == "soldier" || m_scType == "bullet") && interpolation != 0)
{
    m_gameObject.transform.localPosition = Vector3.Lerp(m_fixv3LastPosition.ToVector3(), m_fixv3LogicPosition.ToVector3(), interpolation);
}
else
{
    m_gameObject.transform.localPosition = m_fixv3LogicPosition.ToVector3();
}
```

##<font size = 5>定点数</font>
>定点数和浮点数，是指在计算机中一个数的小数点的位置是固定的还是浮动的,如果一个数中小数点的位置是固定的，则为定点数；如果一个数中小数点的位置是浮动的，则为浮点数。定点数由于小数点的位置固定,因此其精度可控,相反浮点数的精度不可控.

对于帧同步框架来说,定点数是一个非常重要的特性,我们在在不同平台,甚至不同手机上运行一段完全相同的代码时有可能出现截然不同的结果,那是因为不同平台不同cpu对浮点数的处理结果有可能是不一致的,游戏中仅仅0.000000001的精度差距,都可能在多次计算后带来蝴蝶效应,导致完全不同的结果
**举例**:当一个士兵进入塔的攻击范围时,塔会发动攻击,在手机A上的第100帧时,士兵进入了攻击范围,触发了攻击,而在手机B上因为一点点误差,导致101帧时才触发攻击,虽然只差了一帧,但后续会因为这一帧的偏差带来之后更多更大的偏差,从这一帧的不同开始,这已经是两场截然不同的战斗了.
因此我们必须使用定点数来消除精度误差带来的不可预知的结果,让同样的战斗逻辑在任何硬件,任何操作系统下运行都能得到同样的结果.同时也再次印证文章最开始提到的帧同步核心原理:
**相同的输入 + 相同的时机 = 相同的显示**
框架自带了一套完整的定点数库Fix64.cs,其中对浮点数与定点数相互转换,操作符重载都做好了封装,我们可以像使用普通浮点数那样来使用定点数
```
Fix64 a = (Fix64)1;
Fix64 b = (Fix64)2;
Fix64 c = a + b;
```
关于定点数的更多相关细节,请参看文后内容:[哪些unity数据类型不能直接使用](#哪些unity数据类型不能直接使用)


###<font size = 4>关于dotween的正确使用</font>
提及定点数,我们不得不关注一下项目中常用的dotween这个插件,这个插件功能强大,使用非常方便,让我们在做动画时游刃有余,但是如果放到帧同步框架中就不能随便使用了.
上面提到的浮点数精度问题有可能带来巨大的影响,而dotween的整个逻辑都是基于时间帧(Time.deltaTime)插值的,而不是基于帧定长插值,因此不能在涉及到逻辑相关的地方使用,只能用在动画动作渲染相关的地方,比如下面代码就是不能使用的
```
DoLocalMove() function()
	//移动到某个位置后触发会影响后续判断的逻辑
	m_fixMoveTime = Fix64.Zero;
end
```
如果只是渲染表现,而与逻辑运算无关的地方,则可以继续使用dotween.
我们整个帧框架的逻辑运算中没有物理时间的概念,一旦逻辑中涉及到真实物理时间,那肯定会对最终计算的结果造成不可预计的影响,因此类似dotween等动画插件在使用时需要我们多加注意,一个疏忽就会带来整个逻辑运算结果的不一致.

##<font size = 5>随机数</font>
游戏中几乎很难避免使用随机数,恰好随机数也是帧同步框架中一个需要高度关注的注意点,如果每次战斗回放产生的随机数是不一致的,那如何能保证战斗结果是一致的呢,因此我们需要对随机数进行控制,由于不同平台,不同操作系统对随机数的处理方式不同,因此我们避免使用平台自带的随机数接口,而是使用自定义的可控随机数算法SRandom.cs来替代,保证随机数的产生在跨平台方面不会出现问题.同时我们需要记录下每场战斗的随机数种子,只要确定了种子,那产生的随机数序列就一定是一致的.
部分代码片段:
```
// range:[min~(max-1)]
public uint Range(uint min, uint max)
{
    if (min > max)
        throw new ArgumentOutOfRangeException("minValue", string.Format("'{0}' cannot be greater than {1}.", min, max));

    uint num = max - min;
    return Next(num) + min;
}

public int Next(int max)
{
    return (int)(Next() % max);
}
```

#服务器同步校验
服务器校验和同步运算在现在的游戏中应用的越来越广泛,既然要让服务器运行相关的核心代码,那么这部分客户端与服务器共用的逻辑就有一些需要注意的地方.
<li>[逻辑与渲染进行分离](#逻辑和渲染如何进行分离)
<li>[逻辑代码版本控制策略](#逻辑代码版本控制策略)
<li>[避免直接使用Unity特定的数据类型](#哪些unity数据类型不能直接使用)
<li>[避免直接调用Unity特定的接口](#哪些unity接口不能直接调用)

<span id = "逻辑和渲染如何进行分离"></span>
##<font size = 5>逻辑和渲染如何进行分离</font>
服务器是没有渲染的,它只能执行纯逻辑,因此我们的逻辑代码中如何做到逻辑和渲染完全分离就很重要

虽然我们在进行模式设计和代码架构的过程中会尽量做到让逻辑和渲染解耦,独立运行*(具体实现请参见sample源码),*但出于维护同一份逻辑代码的考量,我们并没有办法完全把部分逻辑代码进行隔离,因此怎么识别当前运行环境是客户端还是服务器就很必要了

unity给我们提供了自定义宏定义开关的方法,我们可以通过这个开关来判断当前运行平台是否为客户端,同时关闭服务器代码中不需要执行的渲染部分
![](https://img-blog.csdn.net/20180827093710584?watermark/2/text/aHR0cHM6Ly9ibG9nLmNzZG4ubmV0L3dhbnppMjE1/font/5a6L5L2T/fontsize/400/fill/I0JBQkFCMA==/dissolve/70)
我们可以在unity中Build Settings--Player Settings--Other Settings中找到**Scripting Define Symbols**选项,在其中填入
```
_CLIENTLOGIC_
```
宏定义开关,这样在unity中我们便可以此作为是否为客户端逻辑的判断,在客户端中打开与渲染相关的代码,同时也让服务器逻辑不会受到与渲染相关逻辑的干扰,比如:
```
#if _CLIENTLOGIC_
        m_gameObject.transform.localPosition = position.ToVector3();
#endif
```

<span id = "逻辑代码版本控制策略"></span>
##<font size = 5>逻辑代码版本控制策略</font>
<li>版本控制:
同步校验的关键在于客户端服务器执行的是完全同一份逻辑源码,我们应该极力避免源码来回拷贝的情况出现,因此如何进行版本控制也是需要策略的,在我们公司项目中,需要服务器和客户端同时运行的代码是以git子模块的形式进行管理的,双端各自有自己的业务逻辑,但子模块是相同的,这样维护起来就很方便,推荐大家尝试.

<li>不同服务器架构如何适配:
客户端是c#语言写的,如果服务器也是采用的c#语言,那正好可以无缝结合,共享逻辑,但目前采用c#作为游戏服务器主要语言的项目其实很少,大多是java,c++,golang等,比如我们公司用的是skynet,如果是这种不同语言架构的环境,那我们就需要单独搭建一个c#服务器了,目前我们的做法是在fedora下结合mono搭建的战斗校验服务器,网关收到战斗校验请求后会转发到校验服务器进行战斗校验,把校验结果返回给客户端,具体的方式请参阅后文:[战斗校验服务器简单搭建指引](#战斗校验服务器简单搭建指引)

<span id = "哪些unity数据类型不能直接使用"></span>
##<font size = 5>哪些unity数据类型不能直接使用</font>
<li>float
<li>Vector2
<li>Vector3
上面这三种类型由于都涉及到浮点数,会让逻辑运行结果不可控,因此都不能在帧同步相关的逻辑代码中直接使用,用于替代的是在Fix64.cs中定义的定点数类型:
| 原始数据类型      |    替代数据类型|
| :-------- | --------:|
| float                | Fix64 |
| Vector2           |   FixVector2 |
| Vector3           |    FixVector3 |


同时还有一种例外的情况,某些情况下我们会用Vector2来存放int型对象,在客户端这是没问题的,因为int对象不存在精度误差问题,但是遗憾的是服务器并无法识别Vector2这个unity中的内置数据类型,因此我们不能直接调用,而是需要自己构建一个类似的数据类型,让构建后的数据类型能够跨平台.
在Fix64.cs中新增了NormalVector2这个数据类型用于替代这些unity原生的数据类型,这样就可以同时在客户端和服务器两端运行同样的逻辑代码了.
那项目中是不是完全没有float,没有Vector3这些类型了呢,其实也不完全是,比如设置颜色等API调用还是需要使用float的:
```
public void setColor(float r, float g, float b)
{
#if _CLIENTLOGIC_
	m_gameObject.GetComponent<SpriteRenderer>().color = new Color(r, g, b, 1);
#endif
}
```
>鉴于项目中既存在浮点数数据类型也存在定点数数据类型,因此在框架中使用了匈牙利命名法进行区分,让所有参与编码的人员能一眼分辨出当前变量是浮点数还是定点数

```
Fix64 m_fixElapseTime = Fix64.Zero;  //前缀fix代表该变量为Fix64类型
public FixVector3 m_fixv3LogicPosition = new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.Zero); //前缀fixv3代表该变量为FixVector3类型
float fTime = 0;  //前缀f代表该变量为float类型
```

<span id = "哪些unity接口不能直接调用"></span>
##<font size = 5>哪些unity接口不能直接调用</font>
unity中某些特有的接口不能直接调用,因为服务器环境下并没有这些接口,最常见接口有以下几种:
<li>Debug.Log
<li>PlayerPrefs
<li>Time
不能直接调用不代表不能用,框架中对这些常用接口封装到UnityTools.cs,并用上文提到的____CLIENTLOGIC____开关进行控制,
```
public static void Log(object message)
{
#if _CLIENTLOGIC_
	UnityEngine.Debug.Log(message);
#else
	System.Console.WriteLine (message);
#endif
}

public static void playerPrefsSetString(string key, string value)
{
#if _CLIENTLOGIC_
	PlayerPrefs.SetString(key, value);
#endif
}
```
这样在逻辑代码中调用UnityTools中的接口就可以实现跨平台了
```
UnityTools.Log("end logic frame: " + GameData.g_uGameLogicFrame);
```

#加速功能
实现了基础的帧同步核心功能后,加速功能就很容易实现了,我们只需要改变Time.timeScale这个系统阀值就可以实现.
```
//调整战斗速度
btnAdjustSpeed.onClick.AddListener(delegate ()
{
    if (Time.timeScale == 1)
    {
        Time.timeScale = 2;
        txtAdjustSpeed.text = "2倍速";
    }
    else if (Time.timeScale == 2)
    {
        Time.timeScale = 4;
        txtAdjustSpeed.text = "4倍速";
    }
    else if (Time.timeScale == 4)
    {
        Time.timeScale = 1;
        txtAdjustSpeed.text = "1倍速";
    }
});
```
需要注意的是,由于帧同步的核心原理是在单元片段时间内执行完全相同次数的逻辑运算,从而保证相同输入的结果一定一致,因此在加速后,物理时间内的计算量跟加速的倍数成正比,同样的1秒物理时间片段,加速两倍的计算量是不加速的两倍,加速10倍的运算量是不加速的10倍,因此我们会发现一些性能比较差的设备在加速后会出现明显的卡顿和跳帧的状况,这是CPU运算超负荷的表现,因此需要根据游戏实际的运算量和表现来确定最大加速倍数,以免加速功能影响游戏体验
##<font size = 5>小谈加速优化<font>
实际项目中很容易存在加速后卡顿的问题,这是硬件机能决定的,因此如何在加速后进行优化就很重要,最常见的做法是优化美术效果,把一些不太重要的特效,比如打击效果,buff效果等暂时关掉,加速后会导致各种特效的频繁创建和销毁,开销极大,并且加速后很多细节本来就很难看清楚了,因此根据加速的等级选择性的屏蔽掉一些不影响游戏品质的特效是个不错的思路.由此思路可以引申出一些类似的优化策略,比如停止部分音效的播放,屏蔽实时阴影等小技巧.

#战斗回放功能
通过上面的基础框架的搭建,我们确保了相同的输入一定得到相同的结果,那么战斗回放的问题也就变得相对简单了,我们只需要记录在某个关键游戏帧触发了什么事件就可以了,比如在第100游戏帧,150游戏帧分别触发了**出兵**事件,那我们在回放的时候进行判断,当游戏逻辑帧运行到这两个关键帧时,即调用出兵的API,还原出兵操作,由于操作一致结果必定一致,因此我们就可以看到与原始战斗过程完全一致的战斗回放了.
##<font size = 5>记录战斗关键事件<font>
1.在战斗过程中实时记录
```
GameData.battleInfo info = new GameData.battleInfo();
info.uGameFrame = GameData.g_uGameLogicFrame;
info.sckeyEvent = "createSoldier";
GameData.g_listUserControlEvent.Add(info);
```
2.战斗结束后根据战斗过程中实时记录的信息进行统一保存
```
//- 记录战斗信息(回放时使用)
// 
// @return none
void recordBattleInfo() {
    if (false == GameData.g_bRplayMode) {
        //记录战斗数据
        string content = "";
        for (int i = 0; i < GameData.g_listUserControlEvent.Count; i++)
        {
            GameData.battleInfo v = GameData.g_listUserControlEvent[i];
            //出兵
            if (v.sckeyEvent == "createSoldier") {
                content += v.uGameFrame + "," + v.sckeyEvent + "$";
            }
        }

        UnityTools.playerPrefsSetString("battleRecord", content);
        GameData.g_listUserControlEvent.Clear();
    }
}
```
>Sample为了精简示例流程,战斗日志采用字符串进行存储,用'$'等作为切割标识符,实际项目中可根据实际的网络协议进行制定,比如protobuff,sproto等

##<font size = 5>复原战斗事件<font>
1.把战斗过程中保存的战斗事件进行解码:
```
//- 读取玩家的操作信息
// 
// @return none
void loadUserCtrlInfo()
{
    GameData.g_listPlaybackEvent.Clear();

	string content = battleRecord;

    string[] contents = content.Split('$');

    for (int i = 0; i < contents.Length - 1; i++)
    {
        string[] battleInfo = contents[i].Split(',');

        GameData.battleInfo info = new GameData.battleInfo();

        info.uGameFrame = int.Parse(battleInfo[0]);
        info.sckeyEvent = battleInfo[1];

        GameData.g_listPlaybackEvent.Add(info);
    }
}
```

2.根据解码出来的事件进行逻辑复原:
```
//- 检测回放事件
// 如果有回放事件则进行回放
// @param gameFrame 当前的游戏帧
// @return none
void checkPlayBackEvent(int gameFrame)
{
    if (GameData.g_listPlaybackEvent.Count > 0) {
        for (int i = 0; i < GameData.g_listPlaybackEvent.Count; i++)
        {
            GameData.battleInfo v = GameData.g_listPlaybackEvent[i];

            if (gameFrame == v.uGameFrame) {
                if (v.sckeyEvent == "createSoldier") {
                    createSoldier();
                }
            }
        }
    }
}
```

#框架文件结构
![](https://img-blog.csdn.net/20180901094353592?watermark/2/text/aHR0cHM6Ly9ibG9nLmNzZG4ubmV0L3dhbnppMjE1/font/5a6L5L2T/fontsize/400/fill/I0JBQkFCMA==/dissolve/70)
整个框架中最核心的代码为LockStepLogic.cs(帧同步逻辑),Fix64.cs(定点数)和SRandom.cs(随机数)
其余代码作为一个示例,如何把核心代码运用于实际项目中,并且展示了一个稍微复杂的逻辑如何在帧同步框架下良好运行.
<li>**battle**目录下为帧同步逻辑以及战斗相关的核心代码
<li>**battle/core**为战斗核心代码,其中
	-**action**为自己实现的移动,延迟等基础事件
	-**base**为基础对象,所有战场可见的物体都继承自基础对象
	-**soldier**为士兵相关
	-**state**为状态机相关
	-**tower**为塔相关
<li>**ui**为战斗UI
<li>**view**为视图相关

#自带sample流程
流程:战斗---战斗结束提交操作步骤进行服务器校验---接收服务器校验结果---记录战斗日志---进行战斗回放  
![](https://img-blog.csdn.net/201808270908120?watermark/2/text/aHR0cHM6Ly9ibG9nLmNzZG4ubmV0L3dhbnppMjE1/font/5a6L5L2T/fontsize/400/fill/I0JBQkFCMA==/dissolve/70)
<li>绿色部分为完全相同的战斗逻辑
<li>蓝色部分为完全相同的用户输入


>示例sample中加入了一个非常简单的socket通信功能,用于将客户端的操作发送给服务器,服务器根据客户端的操作进行瞬时回放运算,然后将运算结果发还给客户端进行比对,这里只做了一个最简单的socket功能,力求让整个sample最精简化,实际项目中可根据原有的服务器架构进行替换.

<br>
<br>
<span id = "战斗校验服务器简单搭建指引"></span>
#战斗校验服务器简单搭建指引
<li>安装mono环境
<li>编译可执行文件
<li>实现简单socket通信回传

##<font size = 5>安装mono环境</font>
进入官网https://www.mono-project.com/download/stable/#download-lin-fedora
按照指引进行安装即可

##<font size = 5>编译可执行文件</font>
1.打开刚才安装好的monodeveloper
2.点击file->new->solution
3.在左侧的选项卡中选择Other->.NET
4.在右侧General下选择Console Project
![](https://img-blog.csdn.net/20180830093255771?watermark/2/text/aHR0cHM6Ly9ibG9nLmNzZG4ubmV0L3dhbnppMjE1/font/5a6L5L2T/fontsize/400/fill/I0JBQkFCMA==/dissolve/70)
在左侧工程名上右键导入子模块中battle文件夹下的所有源码
![](https://img-blog.csdn.net/2018083009331084?watermark/2/text/aHR0cHM6Ly9ibG9nLmNzZG4ubmV0L3dhbnppMjE1/font/5a6L5L2T/fontsize/400/fill/I0JBQkFCMA==/dissolve/70)
点击build->Rebuild All,如果编译通过这时会在工程目录下的obj->x86->Debug文件夹下生成可执行文件
如果编译出错请回看上文提到的各种注意点,排查哪里出了问题.
>开发过程中发现工程目录下如果存在git相关的文件会导致monodeveloper报错关闭,如果遇到这种情况需要将工程目录下的.git文件夹和.gitmodules文件进行删除,然后即可正常编译了.

##<font size = 5>运行可执行文件</font>
cmd打开命令行窗口,切换到刚才编译生成的Debug文件目录下,通过mono命令运行编译出来的exe可执行文件
```
mono LockStepFrameWork-server.exe
```
##<font size = 5>服务器端战斗校验逻辑</font>
可执行文件生成后并没有什么实际用处,因为还没有跟我们的战斗逻辑发生联系,我们需要进行一些小小的修改让验证逻辑起作用.
修改新建工程自动生成的Program.cs文件,加入验证代码
```
BattleLogic battleLogic = new BattleLogic ();
battleLogic.init ();
battleLogic.setBattleRecord (battleRecord);
battleLogic.replayVideo();

while (true) {
	battleLogic.updateLogic();
	if (battleLogic.m_bIsBattlePause) {
		break;
	}
}
Console.WriteLine("m_uGameLogicFrame: " + BattleLogic.s_uGameLogicFrame);
```
通过上述代码我们可以看到,首先构建了一个BattleLogic对象,然后传入客户端传过来的操作日志(battleRecord),然后用一个while循环在极短的时间内把战斗逻辑运算了一次,当判断到m_bIsBattlePause为true时证明战斗已结束.
那么我们最后以什么作为战斗校验是否通过的衡量指标呢?很简单,通过游戏逻辑帧s_uGameLogicFrame来进行判断就很准确了,因为只要有一丁点不一致,都不可能跑出完全相同的逻辑帧数,如果想要更保险一点,还可以加入别的与游戏业务逻辑具体相关的参数进行判断,比如杀死的敌人个数,发射了多少颗子弹等等合并作为综合判断依据.

##<font size = 5>实现简单socket通信回传</font>
光有战斗逻辑校验还不够,我们需要加入服务器监听,接收客户端发送过来的战斗日志,计算出结果后再回传给客户端,框架只实现了一段很简单的socket监听和回发消息的功能(尽量将网络通信流程简化,因为大家肯定有自己的一套网络框架和协议),具体请参看Sample源码.
```
Socket serverSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
IPAddress ip = IPAddress.Any;
IPEndPoint point = new IPEndPoint(ip, 2333);
//socket绑定监听地址
serverSocket.Bind(point);
Console.WriteLine("Listen Success");
//设置同时连接个数
serverSocket.Listen(10);

//利用线程后台执行监听,否则程序会假死
Thread thread = new Thread(Listen);
thread.IsBackground = true;
thread.Start(serverSocket);

Console.Read();
```

#框架源码
##<font size = 5>客户端</font>
https://github.com/CraneInForest/LockStepSimpleFramework-Client.git

##<font size = 5>服务器</font>
https://github.com/CraneInForest/LockStepSimpleFramework-Server.git

##<font size = 5>客户端服务器共享逻辑</font>
https://github.com/CraneInForest/LockStepSimpleFramework-Shared.git
>共享逻辑以子模块的形式分别加入到客户端和服务器中,如要运行源码请在clone完毕主仓库后再更新一下子模块,否则没有共享逻辑是无法通过编译的

子模块更新命令:
```
git submodule update --init --recursive
```

>编译环境:
>客户端:win10 + unity5.5.6f1
>服务器:fedora27 64-bit

