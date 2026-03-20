using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Zeye.Sorting.Hub.Domain.Repositories {

    public interface IParcelRepository {
        //查询包裹信息(列表分页)
        //查询包裹详细信息(所有属性)
        //查询包裹前后N条信息(时间顺序)

        //创建包裹信息
        //更新包裹信息
        //删除包裹信息
        //删除过期包裹信息(根据时间删除)
        //批量创建/更新包裹信息

        //查询包裹统计信息(按时间、状态等维度统计)
        //查询包裹异常信息(异常类型、时间等维度统计)
        //查询包裹Noread统计信息(Noread类型、时间等维度统计)

        //按BagCode查询包裹信息
        //按WorkstationName查询包裹信息
        //按状态查询包裹信息
        //按ActualChuteId、TargetChuteId查询包裹信息
    }
}
