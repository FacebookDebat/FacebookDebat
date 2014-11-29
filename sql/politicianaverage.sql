use FacebookDebat;

select
	name,
	count(distinct c.id) as comments,
	sum(case when c.score < -2 then 1 else 0 end) as bad_comments
from FacebookDebat.dbo.Page pa
inner join dbo.Post po on po.page_id = pa.id
inner join dbo.Comment c on po.id = c.post_id
group by pa.id, pa.name